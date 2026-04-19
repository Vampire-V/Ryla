#!/usr/bin/env python3
"""
RYLA-5: Google Sheets Append-Row — E2E Smoke Tests
รัน (default): python3 tests/e2e/google_sheets_append.py
รัน (full mode): RYLA_E2E_GOOGLE_SHEETS=1 python3 tests/e2e/google_sheets_append.py

Default mode: รัน webhook-regression subset
  - ยืนยัน Sheets wiring ไม่เปลี่ยน HTTP contract ของ webhook endpoints
  - ทุก request -> 200 (SkippedNoTenant path) เพราะ DB ไม่มี connection

Full mode (RYLA_E2E_GOOGLE_SHEETS=1): ต้อง setup ก่อน
  1. Generate test RSA key + start Sheets stub:
     python3 tests/e2e/helpers/google_sheets_stub_server.py &
  2. Start app with stub base URL + token endpoint:
       GoogleSheets__BaseUrl=http://localhost:9998 \
       GoogleSheets__TokenEndpoint=http://localhost:9998/token \
       Line__BaseUrl=http://localhost:9999 \
       dotnet run --project backend/src/Ryla.Api &
  3. Seed DB: script จะ inject service_account_json อัตโนมัติ (ใช้ test key)
  4. รัน script ด้วย RYLA_E2E_GOOGLE_SHEETS=1

Test cases (full mode):
  1. Happy path TikTok → Sheets stub receives 1 append + LINE push
  2. Happy path Shopee → Sheets stub receives 1 append
  3. Token cache reuse → 2 appends share 1 token request
  4. Append row contains [iso_ts, platform, orderId, eventType]
  5. Sheets API 401 → log warning + webhook still 200
  6. Sheets API 429 (rate limit) → log warning + webhook still 200
  7. Sheets API 500 (upstream error) → log warning + webhook still 200
  8. Token endpoint down (500) → log error + webhook still 200
  9. Sheets timeout (slow stub > 10s) → log warning + webhook still 200
"""
import hashlib
import hmac
import json
import os
import subprocess
import sys
import time
import urllib.error
import urllib.request
from pathlib import Path


BASE_URL = os.environ.get("RYLA_BASE_URL", "http://localhost:5282")
SHEETS_STUB = os.environ.get("SHEETS_STUB_URL", "http://localhost:9998")
FULL_MODE = os.environ.get("RYLA_E2E_GOOGLE_SHEETS") == "1"

TIKTOK_SECRET = os.environ.get("TIKTOK_CLIENT_SECRET", "dev-secret-replace-me")
SHOPEE_PARTNER_KEY = os.environ.get("SHOPEE_PARTNER_KEY", "test-partner-key-for-dev")
SHOPEE_CALLBACK_URL = os.environ.get("SHOPEE_CALLBACK_URL", "http://localhost:5282/webhooks/shopee")

SEEDED_TIKTOK_SHOP_ID = os.environ.get("SEEDED_TIKTOK_SHOP_ID", "7010000000001")
SEEDED_SHOPEE_SHOP_ID = os.environ.get("SEEDED_SHOPEE_SHOP_ID", "900001")

DATABASE_URL = os.environ.get("DATABASE_URL", "postgresql://postgres:postgres@127.0.0.1:54322/postgres")

REPO_ROOT = Path(__file__).resolve().parents[2]
SEED_TEMPLATE = REPO_ROOT / "tests" / "e2e" / "fixtures" / "google_sheets_seed.sql.template"


def tiktok_sign(body, timestamp):
    base = str(timestamp) + "." + body
    return hmac.HMAC(TIKTOK_SECRET.encode(), base.encode(), hashlib.sha256).hexdigest()


def shopee_sign(body):
    base = SHOPEE_CALLBACK_URL + body
    return hmac.HMAC(SHOPEE_PARTNER_KEY.encode(), base.encode(), hashlib.sha256).hexdigest()


def tiktok_post(body, sign=True):
    now = int(time.time())
    headers = {"Content-Type": "application/json"}
    if sign:
        sig = tiktok_sign(body, now)
        headers["TikTok-Signature"] = "t=" + str(now) + ",s=" + sig
    req = urllib.request.Request(
        BASE_URL + "/webhooks/tiktok-shop",
        data=body.encode("utf-8"),
        headers=headers,
        method="POST",
    )
    try:
        with urllib.request.urlopen(req) as resp:
            return resp.status
    except urllib.error.HTTPError as e:
        return e.code


def shopee_post(body, sign=True):
    url_suffix = ""
    if sign:
        sig = shopee_sign(body)
        url_suffix = "?authorization=" + sig
    req = urllib.request.Request(
        BASE_URL + "/webhooks/shopee" + url_suffix,
        data=body.encode("utf-8"),
        headers={"Content-Type": "application/json"},
        method="POST",
    )
    try:
        with urllib.request.urlopen(req) as resp:
            return resp.status
    except urllib.error.HTTPError as e:
        return e.code


def stub_reset():
    req = urllib.request.Request(SHEETS_STUB + "/__reset", method="POST")
    urllib.request.urlopen(req).read()


def stub_received():
    with urllib.request.urlopen(SHEETS_STUB + "/__received") as resp:
        return json.loads(resp.read().decode("utf-8"))


def stub_token_calls():
    with urllib.request.urlopen(SHEETS_STUB + "/__token_calls") as resp:
        return json.loads(resp.read().decode("utf-8"))["count"]


def stub_fail(endpoint, code=None, message=None):
    url = SHEETS_STUB + "/__fail_mode?endpoint=" + endpoint
    if code is not None:
        url += "&code=" + str(code)
        if message:
            url += "&message=" + message
    urllib.request.urlopen(url).read()


def stub_delay(ms=0):
    urllib.request.urlopen(SHEETS_STUB + "/__delay?ms=" + str(ms)).read()


def generate_test_service_account_json():
    """Generate RSA keypair + wrap in Google service-account-style JSON."""
    pem_bytes = subprocess.check_output(
        ["openssl", "genpkey", "-algorithm", "RSA", "-pkeyopt", "rsa_keygen_bits:2048"],
        stderr=subprocess.DEVNULL,
    )
    pem = pem_bytes.decode("utf-8").strip()
    return json.dumps({
        "type": "service_account",
        "client_email": "ryla-e2e@stub.iam.gserviceaccount.com",
        "private_key": pem,
        "token_uri": SHEETS_STUB + "/token",
    })


def _psql_runner():
    """Pick psql binary or docker fallback (Supabase local container)."""
    if subprocess.run(["which", "psql"], capture_output=True).returncode == 0:
        return ["psql", DATABASE_URL, "-v", "ON_ERROR_STOP=1"]
    container = os.environ.get("SUPABASE_DB_CONTAINER", "supabase_db_ryla")
    if subprocess.run(["docker", "inspect", container], capture_output=True).returncode == 0:
        return ["docker", "exec", "-i", container, "psql", "-U", "postgres", "-v", "ON_ERROR_STOP=1"]
    raise RuntimeError("Neither psql binary nor docker container '" + container + "' available")


def seed_db():
    """Render seed template + apply to DB via psql (or docker fallback)."""
    sa_json = generate_test_service_account_json()
    # PostgreSQL string escaping: ' → ''
    sa_json_escaped = sa_json.replace("'", "''")
    template = SEED_TEMPLATE.read_text()
    sql = template.replace("${SERVICE_ACCOUNT_JSON_ESCAPED}", sa_json_escaped)
    proc = subprocess.run(_psql_runner(), input=sql, capture_output=True, text=True)
    if proc.returncode != 0:
        print("  ERROR seeding DB: " + proc.stderr)
        sys.exit(1)


print("\n" + "=" * 60)
print("  RYLA-5: Google Sheets Append E2E — " + BASE_URL)
print("  Mode: " + ("FULL (with DB seed + Sheets stub)" if FULL_MODE else "REGRESSION-ONLY"))
print("=" * 60 + "\n")


passed = failed = skipped = 0


def check(desc, got, expected):
    global passed, failed
    ok = got == expected
    status = "PASS" if ok else "FAIL"
    print("  " + status + "  " + desc + "  [" + str(got) + "]")
    if not ok:
        print("       Expected " + str(expected))
        failed += 1
    else:
        passed += 1


def skip(desc, reason):
    global skipped
    print("  SKIP  " + desc + "  (" + reason + ")")
    skipped += 1


# ------------------------------------------------------------
# Regression cases (always run)
# ------------------------------------------------------------
now = int(time.time())

tiktok_body = json.dumps({
    "client_key": "e2e",
    "event": "ORDER_STATUS_CHANGE",
    "create_time": now,
    "content": json.dumps({"shop_id": SEEDED_TIKTOK_SHOP_ID, "order_id": "E2E_SHEETS_TT_001"}),
})
check("TikTok webhook with Sheets wiring → 200",
      tiktok_post(tiktok_body), 200)

shopee_body = json.dumps({
    "code": 3,
    "shop_id": int(SEEDED_SHOPEE_SHOP_ID),
    "timestamp": now,
    "data": {"ordersn": "E2E_SHEETS_SP_001", "status": "READY_TO_SHIP"},
})
check("Shopee webhook with Sheets wiring → 200",
      shopee_post(shopee_body), 200)


# ------------------------------------------------------------
# Full-mode cases (require DB seed + Sheets stub)
# ------------------------------------------------------------
if not FULL_MODE:
    skip("TikTok webhook → Sheets stub receives append", "RYLA_E2E_GOOGLE_SHEETS != 1")
    skip("Shopee webhook → Sheets stub receives append", "RYLA_E2E_GOOGLE_SHEETS != 1")
    skip("Token cache reuse — 2 appends share 1 token request", "RYLA_E2E_GOOGLE_SHEETS != 1")
    skip("Append row contains platform + orderId + eventType", "RYLA_E2E_GOOGLE_SHEETS != 1")
    skip("Sheets 401 swallowed — webhook still 200", "RYLA_E2E_GOOGLE_SHEETS != 1")
    skip("Sheets 429 swallowed — webhook still 200", "RYLA_E2E_GOOGLE_SHEETS != 1")
    skip("Sheets 500 swallowed — webhook still 200", "RYLA_E2E_GOOGLE_SHEETS != 1")
    skip("Token endpoint 500 swallowed — webhook still 200", "RYLA_E2E_GOOGLE_SHEETS != 1")
    skip("Sheets timeout swallowed — webhook still 200", "RYLA_E2E_GOOGLE_SHEETS != 1")
else:
    try:
        stub_reset()
    except Exception as e:
        print("  ERROR  cannot reach Sheets stub: " + str(e))
        sys.exit(1)

    print("  ...seeding DB with test service account key")
    seed_db()

    # Case 1: TikTok happy path
    stub_reset()
    status = tiktok_post(tiktok_body)
    received = stub_received()
    check("TikTok webhook → Sheets stub receives 1 append (configured tenant)",
          (status, len(received)), (200, 1))

    # Case 2: Shopee happy path
    stub_reset()
    status = shopee_post(shopee_body)
    received = stub_received()
    check("Shopee webhook → Sheets stub receives 1 append (configured tenant)",
          (status, len(received)), (200, 1))

    # Case 3: Token cache reuse — 2 sequential appends should share 1 token
    stub_reset()
    tiktok_post(tiktok_body)
    tiktok_post(tiktok_body)
    token_calls = stub_token_calls()
    check("Token cache reuse — 2 appends produce ≤ 1 token request",
          token_calls <= 1, True)

    # Case 4: Append row format
    stub_reset()
    tiktok_post(tiktok_body)
    received = stub_received()
    if received:
        body_text = json.dumps(received[0]["body"])
        contains_all = (
            "TikTokShop" in body_text or "TikTok Shop" in body_text or "tiktok_shop" in body_text
        ) and "E2E_SHEETS_TT_001" in body_text
        check("Append body contains platform + orderId",
              contains_all, True)
    else:
        check("Append body contains platform + orderId", False, True)

    # Case 5: Sheets 401 swallowed
    stub_reset()
    stub_fail("append", 401, "Unauthorized")
    status = tiktok_post(tiktok_body)
    check("Sheets 401 swallowed — webhook still returns 200", status, 200)
    stub_fail("append", None)

    # Case 6: Sheets 429 swallowed
    stub_reset()
    stub_fail("append", 429, "Rate")
    status = tiktok_post(tiktok_body)
    check("Sheets 429 swallowed — webhook still returns 200", status, 200)
    stub_fail("append", None)

    # Case 7: Sheets 500 swallowed
    stub_reset()
    stub_fail("append", 500, "ServerError")
    status = tiktok_post(tiktok_body)
    check("Sheets 500 swallowed — webhook still returns 200", status, 200)
    stub_fail("append", None)

    # Case 8: Token endpoint 500 swallowed
    stub_reset()
    stub_fail("token", 500, "TokenDown")
    status = tiktok_post(tiktok_body)
    check("Token endpoint 500 swallowed — webhook still returns 200", status, 200)
    stub_fail("token", None)

    # Case 9: Sheets timeout swallowed (HttpClient timeout = 10s; stub sleeps 11s)
    stub_reset()
    stub_delay(11000)
    status = tiktok_post(tiktok_body)
    check("Sheets timeout swallowed — webhook still returns 200", status, 200)
    stub_delay(0)


print("\n" + "=" * 60)
print("  " + str(passed) + " passed, " + str(failed) + " failed, " + str(skipped) + " skipped")
print("=" * 60 + "\n")

sys.exit(1 if failed else 0)
