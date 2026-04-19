#!/usr/bin/env python3
"""
RYLA-3: Shopee Webhook Receiver — E2E Smoke Tests
รัน: python3 tests/e2e/webhook_shopee.py
หรือ: make test-e2e (app ต้องรันอยู่ที่ localhost:5282)

SHOPEE_PARTNER_KEY อ่านจาก env var หรือใช้ dev default
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
PARTNER_KEY = os.environ.get("SHOPEE_PARTNER_KEY", "test-partner-key-for-dev")
CALLBACK_URL = os.environ.get("SHOPEE_CALLBACK_URL", "http://localhost:5282/webhooks/shopee")
ENDPOINT = BASE_URL + "/webhooks/shopee"


def compute_hmac(body):
    base_string = CALLBACK_URL + body
    return hmac.new(PARTNER_KEY.encode(), base_string.encode(), hashlib.sha256).hexdigest()


def post(body, authorization=None):
    data = body.encode("utf-8")
    url = ENDPOINT
    if authorization is not None:
        url += "?authorization=" + authorization
    headers = {"Content-Type": "application/json"}
    req = urllib.request.Request(url, data=data, headers=headers, method="POST")
    try:
        with urllib.request.urlopen(req) as resp:
            return resp.status
    except urllib.error.HTTPError as e:
        return e.code


now = int(time.time())
valid_body = json.dumps({
    "code": 3,
    "shop_id": 123456,
    "timestamp": now,
    "data": {
        "ordersn": "E2E_TEST_ORDER",
        "status": "READY_TO_SHIP",
        "update_time": now,
    },
})
other_event_body = json.dumps({
    "code": 0,
    "shop_id": 123456,
    "timestamp": now,
    "data": {"shop_id": 123456},
})

cases = [
    # (description, body, authorization, expected_status)
    ("Valid order webhook -> 200",
     valid_body,
     compute_hmac(valid_body),
     200),
    ("Wrong signature -> 401",
     valid_body,
     "dead" * 16,
     401),
    ("Missing authorization param -> 401",
     valid_body,
     None,
     401),
    ("Expired timestamp (-10 min) -> 401",
     json.dumps({"code": 3, "shop_id": 123, "timestamp": now - 600, "data": {}}),
     compute_hmac(json.dumps({"code": 3, "shop_id": 123, "timestamp": now - 600, "data": {}})),
     401),
    ("Future timestamp (+10 min) -> 401",
     json.dumps({"code": 3, "shop_id": 123, "timestamp": now + 600, "data": {}}),
     compute_hmac(json.dumps({"code": 3, "shop_id": 123, "timestamp": now + 600, "data": {}})),
     401),
    ("Malformed JSON with valid sig -> 422",
     "not-json",
     compute_hmac("not-json"),
     422),
    ("Empty body -> 401",
     "",
     compute_hmac(""),
     401),
    ("Different event type (code 0) -> 200",
     other_event_body,
     compute_hmac(other_event_body),
     200),
    ("Tampered body with original sig -> 401",
     "tampered",
     compute_hmac(valid_body),
     401),
]

print("\n" + "=" * 60)
print("  RYLA-3: Shopee Webhook E2E — " + ENDPOINT)
print("=" * 60 + "\n")

passed = failed = 0
for desc, body, auth, expected in cases:
    got = post(body, auth)
    ok = got == expected
    status = "PASS" if ok else "FAIL"
    print("  " + status + "  " + desc + "  [" + str(got) + "]")
    if not ok:
        print("       Expected " + str(expected))
        failed += 1
    else:
        passed += 1

print("\n" + "=" * 60)
print("  " + str(passed) + "/" + str(passed + failed) + " passed")
print("=" * 60 + "\n")

sys.exit(1 if failed else 0)
