#!/usr/bin/env python3
"""
RYLA-2: TikTok Shop Webhook Receiver — E2E Smoke Tests
รัน: python3 tests/e2e/webhook_tiktok_shop.py
หรือ: make test-e2e (app ต้องรันอยู่ที่ localhost:5282)

CLIENT_SECRET อ่านจาก env var TIKTOK_CLIENT_SECRET หรือใช้ dev default
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
CLIENT_SECRET = os.environ.get("TIKTOK_CLIENT_SECRET", "dev-secret-replace-me")
ENDPOINT = BASE_URL + "/webhooks/tiktok-shop"
SIGNATURE_HEADER = "TikTok-Signature"


def compute_hmac(body, timestamp):
    payload = str(timestamp) + "." + body
    return hmac.new(CLIENT_SECRET.encode(), payload.encode(), hashlib.sha256).hexdigest()


def post(body, sig_header):
    data = body.encode("utf-8")
    headers = {"Content-Type": "application/json"}
    if sig_header:
        headers[SIGNATURE_HEADER] = sig_header
    req = urllib.request.Request(ENDPOINT, data=data, headers=headers, method="POST")
    try:
        with urllib.request.urlopen(req) as resp:
            return resp.status
    except urllib.error.HTTPError as e:
        return e.code


now = int(time.time())
valid_body = json.dumps({
    "client_key": "e2e",
    "event": "ORDER_STATUS_CHANGE",
    "create_time": now,
    "content": "{}",
})
other_body = json.dumps({
    "client_key": "test",
    "event": "PRODUCT_UPDATE",
    "create_time": now,
    "content": "{}",
})

cases = [
    # (description, body, signature_header, expected_status)
    ("Valid signature → 200",
     valid_body,
     "t=" + str(now) + ",s=" + compute_hmac(valid_body, now),
     200),
    ("Wrong signature → 401",
     valid_body,
     "t=" + str(now) + ",s=" + "dead" * 16,
     401),
    ("Missing header → 401",
     valid_body,
     None,
     401),
    ("Expired timestamp (-10 min) → 401",
     valid_body,
     "t=" + str(now - 600) + ",s=" + compute_hmac(valid_body, now - 600),
     401),
    ("Future timestamp (+10 min) → 401",
     valid_body,
     "t=" + str(now + 600) + ",s=" + compute_hmac(valid_body, now + 600),
     401),
    ("Malformed header → 401",
     valid_body,
     "not-a-valid-header",
     401),
    ("Invalid JSON with valid sig → 422",
     "not-json",
     "t=" + str(now) + ",s=" + compute_hmac("not-json", now),
     422),
    ("Different event type → 200",
     other_body,
     "t=" + str(now) + ",s=" + compute_hmac(other_body, now),
     200),
    ("Tampered body with original sig → 401",
     "tampered",
     "t=" + str(now) + ",s=" + compute_hmac(valid_body, now),
     401),
]

print("\n" + "=" * 60)
print("  RYLA-2: TikTok Shop Webhook E2E — " + ENDPOINT)
print("=" * 60 + "\n")

passed = failed = 0
for desc, body, sig, expected in cases:
    got = post(body, sig)
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
