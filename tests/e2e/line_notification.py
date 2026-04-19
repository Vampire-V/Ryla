#!/usr/bin/env python3
"""
RYLA-4: LINE Push Notification — E2E Smoke Tests
รัน (default): python3 tests/e2e/line_notification.py
รัน (full mode): RYLA_E2E_LINE=1 python3 tests/e2e/line_notification.py

Default mode: รัน webhook-regression subset ที่ไม่ต้องพึ่ง DB seed
  - ยืนยันว่า LINE wiring ไม่เปลี่ยน HTTP contract ของ endpoints
  - ทุก request -> 200 (SkippedNoTenant path) เพราะ DB ไม่มี connection

Full mode (RYLA_E2E_LINE=1): ต้อง setup ก่อน
  1. Start LINE stub:  python3 tests/e2e/helpers/line_stub_server.py &
  2. Start app with stub base URL:
       Line__BaseUrl=http://localhost:9999 dotnet run --project backend/src/Ryla.Api
  3. Seed DB: psql "$DATABASE_URL" -f tests/e2e/fixtures/line_notification_seed.sql
  4. รัน script นี้ด้วย RYLA_E2E_LINE=1

Test cases:
  - Regression (ทั้ง default + full): webhook ยัง verify HMAC ถูกต้อง
  - Full mode only: LINE stub receives push + skip paths + error handling
"""
import hmac
import hashlib
import json
import os
import sys
import time
import urllib.request
import urllib.error


BASE_URL = os.environ.get("RYLA_BASE_URL", "http://localhost:5282")
LINE_STUB = os.environ.get("LINE_STUB_URL", "http://localhost:9999")
FULL_MODE = os.environ.get("RYLA_E2E_LINE") == "1"

TIKTOK_SECRET = os.environ.get("TIKTOK_CLIENT_SECRET", "dev-secret-replace-me")
SHOPEE_PARTNER_KEY = os.environ.get("SHOPEE_PARTNER_KEY", "test-partner-key-for-dev")
SHOPEE_CALLBACK_URL = os.environ.get("SHOPEE_CALLBACK_URL", "http://localhost:5282/webhooks/shopee")

SEEDED_TIKTOK_SHOP_ID = os.environ.get("SEEDED_TIKTOK_SHOP_ID", "7010000000001")
SEEDED_SHOPEE_SHOP_ID = os.environ.get("SEEDED_SHOPEE_SHOP_ID", "900001")


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


def line_reset():
    req = urllib.request.Request(LINE_STUB + "/__reset", method="POST")
    urllib.request.urlopen(req).read()


def line_received():
    with urllib.request.urlopen(LINE_STUB + "/__received") as resp:
        return json.loads(resp.read().decode("utf-8"))


def line_fail_mode(code=None, message=None):
    url = LINE_STUB + "/__fail_mode"
    if code is not None:
        url += "?code=" + str(code)
        if message:
            url += "&message=" + message
    urllib.request.urlopen(url).read()


print("\n" + "=" * 60)
print("  RYLA-4: LINE Notification E2E — " + BASE_URL)
print("  Mode: " + ("FULL (with DB seed + LINE stub)" if FULL_MODE else "REGRESSION-ONLY"))
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
# Regression cases (always run — validate wiring doesn't break HTTP contract)
# ------------------------------------------------------------
now = int(time.time())

tiktok_body = json.dumps({
    "client_key": "e2e",
    "event": "ORDER_STATUS_CHANGE",
    "create_time": int(time.time()),
    "content": json.dumps({"shop_id": SEEDED_TIKTOK_SHOP_ID, "order_id": "E2E_TIKTOK_001"}),
})
check("TikTok webhook with LINE wiring → 200",
      tiktok_post(tiktok_body), 200)

shopee_body = json.dumps({
    "code": 3,
    "shop_id": int(SEEDED_SHOPEE_SHOP_ID),
    "timestamp": now,
    "data": {"ordersn": "E2E_SHOPEE_001", "status": "READY_TO_SHIP"},
})
check("Shopee webhook with LINE wiring → 200",
      shopee_post(shopee_body), 200)

check("TikTok with tampered body → 401 (regression, no use case call)",
      tiktok_post(json.dumps({"tampered": True}), sign=False), 401)

check("Shopee with missing signature → 401 (regression, no use case call)",
      shopee_post(shopee_body, sign=False), 401)


# ------------------------------------------------------------
# Full-mode cases (require DB seed + LINE stub)
# ------------------------------------------------------------
if not FULL_MODE:
    skip("TikTok webhook → LINE stub receives push (configured tenant)",
         "RYLA_E2E_LINE != 1")
    skip("Shopee webhook → LINE stub receives push (configured tenant)",
         "RYLA_E2E_LINE != 1")
    skip("Push body contains platform name + order id",
         "RYLA_E2E_LINE != 1")
    skip("LINE 401 swallowed — webhook still returns 200",
         "RYLA_E2E_LINE != 1")
else:
    try:
        line_reset()
    except Exception as e:
        print("  ERROR  cannot reach LINE stub: " + str(e))
        sys.exit(1)

    # Case: TikTok happy path
    line_reset()
    status = tiktok_post(tiktok_body)
    received = line_received()
    check("TikTok webhook → LINE stub receives push (configured tenant)",
          (status, len(received)), (200, 1))
    if received:
        body_text = json.dumps(received[0])
        assert_text = "TikTok Shop" in body_text and "E2E_TIKTOK_001" in body_text
        check("Push body contains platform name + order id",
              assert_text, True)

    # Case: Shopee happy path
    line_reset()
    status = shopee_post(shopee_body)
    received = line_received()
    check("Shopee webhook → LINE stub receives push (configured tenant)",
          (status, len(received)), (200, 1))

    # Case: LINE 401 swallowed
    line_reset()
    line_fail_mode(401, "Unauthorized")
    status = tiktok_post(tiktok_body)
    check("LINE 401 swallowed — webhook still returns 200",
          status, 200)
    line_fail_mode(None)


print("\n" + "=" * 60)
print("  " + str(passed) + " passed, " + str(failed) + " failed, " + str(skipped) + " skipped")
print("=" * 60 + "\n")

sys.exit(1 if failed else 0)
