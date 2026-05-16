#!/usr/bin/env python3
"""
RYLA-9: Profit Dashboard API — E2E Smoke Tests
รัน: python3 tests/e2e/profit_dashboard.py
หรือ: make test-e2e (app ต้องรันอยู่ที่ localhost:5282)

Full mode (ต้อง seed tenant): RYLA_E2E_PROFIT=1 RYLA_TEST_TENANT_ID=<uuid>
"""
import json
import hmac
import hashlib
import os
import sys
import time
import urllib.request
import urllib.error
from uuid import uuid4

BASE_URL = os.environ.get("RYLA_BASE_URL", "http://localhost:5282")
PARTNER_KEY = os.environ.get("SHOPEE_PARTNER_KEY", "test-partner-key-for-dev")
CALLBACK_URL = os.environ.get("SHOPEE_CALLBACK_URL", "http://localhost:5282/webhooks/shopee")
SHOPEE_WEBHOOK = BASE_URL + "/webhooks/shopee"
FULL_MODE = os.environ.get("RYLA_E2E_PROFIT", "").lower() in ("1", "true", "yes")

# ถ้าต้องการ full mode ต้องมี tenant จริงในฐานข้อมูล
TEST_TENANT_ID = os.environ.get("RYLA_TEST_TENANT_ID", str(uuid4()))
FAKE_TENANT_ID = str(uuid4())  # UUID ที่ไม่มีในฐานข้อมูล


def get(path, *, tenant_id=None, params="", follow_redirects=True):
    url = BASE_URL + path
    if params:
        url += "?" + params
    headers = {"Content-Type": "application/json"}
    if tenant_id:
        headers["X-Tenant-Id"] = tenant_id
    req = urllib.request.Request(url, headers=headers, method="GET")
    opener = urllib.request.build_opener() if follow_redirects else \
        urllib.request.build_opener(NoRedirectHandler())
    try:
        with opener.open(req) as resp:
            return resp.status, resp.read().decode()
    except urllib.error.HTTPError as e:
        return e.code, e.read().decode()


class NoRedirectHandler(urllib.request.HTTPRedirectHandler):
    def redirect_request(self, req, fp, code, msg, headers, newurl):
        raise urllib.error.HTTPError(req.full_url, code, msg, headers, fp)


def post(path, body, *, tenant_id=None):
    url = BASE_URL + path
    data = json.dumps(body).encode("utf-8")
    headers = {"Content-Type": "application/json"}
    if tenant_id:
        headers["X-Tenant-Id"] = tenant_id
    req = urllib.request.Request(url, data=data, headers=headers, method="POST")
    try:
        with urllib.request.urlopen(req) as resp:
            return resp.status, resp.read().decode()
    except urllib.error.HTTPError as e:
        return e.code, e.read().decode()


def shopee_webhook(body_dict, *, corrupt_sig=False):
    body = json.dumps(body_dict)
    sig = hmac.HMAC(PARTNER_KEY.encode(), (CALLBACK_URL + body).encode(), hashlib.sha256).hexdigest()
    if corrupt_sig:
        sig = "dead" * 16
    url = SHOPEE_WEBHOOK + "?authorization=" + sig
    data = body.encode("utf-8")
    req = urllib.request.Request(url, data=data, headers={"Content-Type": "application/json"}, method="POST")
    try:
        with urllib.request.urlopen(req) as resp:
            return resp.status
    except urllib.error.HTTPError as e:
        return e.code


now = int(time.time())
test_order_sn = "E2E_PROFIT_" + str(now)

# ─────────────────────────────────────────────────────────────────────────────
# Test cases: (description, function, expected_pass_condition)
# ─────────────────────────────────────────────────────────────────────────────

def run_case(desc, fn, condition):
    try:
        result = fn()
        ok = condition(result)
        return ok, result, None
    except Exception as e:
        return False, None, str(e)


cases = []

# ─── Validation: missing X-Tenant-Id → 400 ───────────────────────────────────

cases.append((
    "summary_no_header: GET /api/profit/summary sans X-Tenant-Id → 400",
    lambda: get("/api/profit/summary"),
    lambda r: r[0] == 400,
))

cases.append((
    "orders_no_header: GET /api/profit/orders sans X-Tenant-Id → 400",
    lambda: get("/api/profit/orders"),
    lambda r: r[0] == 400,
))

cases.append((
    "sku_costs_no_header: GET /api/profit/sku-costs sans X-Tenant-Id → 400",
    lambda: get("/api/profit/sku-costs"),
    lambda r: r[0] == 400,
))

cases.append((
    "sync_status_no_header: GET /api/profit/sync-status sans X-Tenant-Id → 400",
    lambda: get("/api/profit/sync-status"),
    lambda r: r[0] == 400,
))

# ─── Validation: POST sku-costs input validation ──────────────────────────────

cases.append((
    "sku_costs_cogs_zero: POST cogs=0 → 422 (cogs must be > 0)",
    lambda: post("/api/profit/sku-costs",
                 {"itemSku": "TEST-SKU", "cogs": 0},
                 tenant_id=FAKE_TENANT_ID),
    lambda r: r[0] == 422,
))

cases.append((
    "sku_costs_empty_sku: POST itemSku='' → 422",
    lambda: post("/api/profit/sku-costs",
                 {"itemSku": "", "cogs": 100},
                 tenant_id=FAKE_TENANT_ID),
    lambda r: r[0] == 422,
))

# ─── OAuth endpoints ──────────────────────────────────────────────────────────

cases.append((
    "oauth_authorize_no_tenant: GET /api/shopee/oauth/authorize sans tenant_id → 400",
    lambda: get("/api/shopee/oauth/authorize"),
    lambda r: r[0] == 400,
))

cases.append((
    "oauth_authorize_valid: GET /api/shopee/oauth/authorize with UUID → 302 to Shopee",
    lambda: get("/api/shopee/oauth/authorize", params="tenant_id=" + FAKE_TENANT_ID,
                follow_redirects=False),
    lambda r: r[0] == 302,
))

# ─── Webhook security: tampered payload ───────────────────────────────────────

cases.append((
    "invalid_webhook_signature: webhook with bad sig → 401",
    lambda: shopee_webhook({
        "code": 3, "shop_id": 999, "timestamp": now,
        "data": {"ordersn": test_order_sn, "status": "COMPLETED"}
    }, corrupt_sig=True),
    lambda r: r == 401,
))

# ─── Webhook idempotency ───────────────────────────────────────────────────────

cases.append((
    "webhook_backfill_race: same order_sn webhook sent twice → both 200 (upsert idempotent)",
    lambda: (
        shopee_webhook({
            "code": 3, "shop_id": 999, "timestamp": now,
            "data": {"ordersn": test_order_sn, "status": "SHIPPED"}
        }),
        shopee_webhook({
            "code": 3, "shop_id": 999, "timestamp": now,
            "data": {"ordersn": test_order_sn, "status": "COMPLETED"}
        })
    ),
    lambda r: r[0] == 200 and r[1] == 200,
))

# ─── Full mode: functional tests (ต้องการ seed tenant) ───────────────────────

if FULL_MODE:
    cases.append((
        "summary_empty_returns_zeros: GET /summary with real tenant → 200 zero counts",
        lambda: get("/api/profit/summary", tenant_id=TEST_TENANT_ID, params="range=30d"),
        lambda r: r[0] == 200 and '"completedOrderCount"' in r[1],
    ))
else:
    cases.append((
        "summary_structure_check: [SKIP in regression] GET /summary response structure",
        lambda: ("skipped", ""),
        lambda r: r[0] == "skipped",
    ))

# ─────────────────────────────────────────────────────────────────────────────
print()
print("=" * 65)
print("  RYLA-9: Profit Dashboard E2E — " + BASE_URL)
if FULL_MODE:
    print("  Mode: FULL (tenant=" + TEST_TENANT_ID + ")")
else:
    print("  Mode: REGRESSION (no DB seed required)")
print("=" * 65)
print()

passed = failed = skipped = 0
for desc, fn, condition in cases:
    ok, result, err = run_case(desc, fn, condition)

    if result and isinstance(result, tuple) and result[0] == "skipped":
        status = "SKIP"
        skipped += 1
    elif ok:
        status = "PASS"
        passed += 1
    else:
        status = "FAIL"
        failed += 1

    print(f"  {status}  {desc}")
    if not ok and status != "SKIP":
        if err:
            print(f"       Error: {err}")
        elif result:
            print(f"       Got: {result[0] if isinstance(result, tuple) else result}")

print()
print("=" * 65)
total = passed + failed
print(f"  {passed}/{total} passed" + (f", {skipped} skipped" if skipped else ""))
print("=" * 65)
print()

sys.exit(1 if failed else 0)
