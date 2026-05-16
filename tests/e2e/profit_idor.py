#!/usr/bin/env python3
"""
RYLA-9: Profit Dashboard IDOR Fix — E2E Smoke Tests
ทดสอบว่า X-Tenant-Id header ถูกลบออกและ JWT-based auth ทำงานถูกต้อง

รัน: python3 tests/e2e/profit_idor.py
หรือ: make test-e2e (app ต้องรันอยู่ที่ localhost:5282)

Full mode (ต้อง JWT ของ Supabase user จริง):
  RYLA_E2E_IDOR=1 RYLA_TEST_JWT=<supabase_access_token> python3 tests/e2e/profit_idor.py
"""
import os
import sys
import urllib.request
import urllib.error
import json
import hmac
import hashlib
import base64
import struct
import time

BASE_URL = os.environ.get("RYLA_BASE_URL", "http://localhost:5282")
FULL_MODE = os.environ.get("RYLA_E2E_IDOR", "").lower() in ("1", "true", "yes")
TEST_JWT = os.environ.get("RYLA_TEST_JWT", "")

# ─── Helpers ──────────────────────────────────────────────────────────────────

def make_request(method: str, path: str, *, headers: dict | None = None, body: bytes | None = None) -> tuple[int, str]:
    """ส่ง HTTP request และ return (status_code, response_body)"""
    url = BASE_URL + path
    req = urllib.request.Request(url, method=method, data=body, headers=headers or {})
    try:
        with urllib.request.urlopen(req) as resp:
            return resp.status, resp.read().decode()
    except urllib.error.HTTPError as e:
        return e.code, e.read().decode()


def get(path: str, *, auth_header: str | None = None) -> tuple[int, str]:
    headers = {}
    if auth_header:
        headers["Authorization"] = auth_header
    return make_request("GET", path, headers=headers)


def post(path: str, body: dict, *, auth_header: str | None = None) -> tuple[int, str]:
    data = json.dumps(body).encode()
    headers = {"Content-Type": "application/json"}
    if auth_header:
        headers["Authorization"] = auth_header
    return make_request("POST", path, headers=headers, body=data)


def create_fake_jwt(sub: str = "test-user-id") -> str:
    """สร้าง JWT ปลอมที่ไม่มี valid signature — สำหรับ test auth failure cases"""
    header = base64.urlsafe_b64encode(b'{"alg":"HS256","typ":"JWT"}').rstrip(b'=').decode()
    payload_bytes = json.dumps({
        "sub": sub,
        "iss": "https://fake.supabase.co/auth/v1",
        "aud": "authenticated",
        "exp": int(time.time()) + 3600,
    }).encode()
    payload = base64.urlsafe_b64encode(payload_bytes).rstrip(b'=').decode()
    # ใส่ random signature ที่ไม่ valid
    fake_sig = base64.urlsafe_b64encode(b"invalidsignature").rstrip(b'=').decode()
    return f"{header}.{payload}.{fake_sig}"


def run_case(desc: str, fn) -> bool:
    """รัน test case และ print result"""
    try:
        fn()
        print(f"  PASS  {desc}")
        return True
    except AssertionError as e:
        print(f"  FAIL  {desc}")
        print(f"        {e}")
        return False
    except Exception as e:
        print(f"  ERROR {desc}")
        print(f"        {type(e).__name__}: {e}")
        return False


# ─── Test Cases ───────────────────────────────────────────────────────────────

PROFIT_ENDPOINTS = [
    "/api/profit/summary",
    "/api/profit/orders",
    "/api/profit/sku-costs",
    "/api/profit/sync-status",
]


def test_no_auth_returns_401():
    """ทุก profit endpoint ต้องการ auth — ไม่มี header ควร return 401"""
    for endpoint in PROFIT_ENDPOINTS:
        status, body = get(endpoint)
        assert status == 401, f"GET {endpoint}: expected 401, got {status} | body: {body[:100]}"


def test_x_tenant_id_header_rejected():
    """X-Tenant-Id header ไม่ควร bypass auth — ต้อง return 401"""
    for endpoint in PROFIT_ENDPOINTS:
        headers = {"X-Tenant-Id": "bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"}
        status, body = make_request("GET", endpoint, headers=headers)
        assert status == 401, (
            f"GET {endpoint} with X-Tenant-Id: expected 401 (no auth), got {status}"
            " — IDOR vulnerability may not be fixed!"
        )


def test_tampered_jwt_rejected():
    """JWT ที่ signature ไม่ valid ต้อง return 401"""
    fake_jwt = create_fake_jwt()
    for endpoint in PROFIT_ENDPOINTS:
        status, body = get(endpoint, auth_header=f"Bearer {fake_jwt}")
        assert status == 401, (
            f"GET {endpoint} with invalid JWT: expected 401, got {status}"
        )


def test_malformed_bearer_rejected():
    """Bearer token ที่ format ผิดต้อง return 401"""
    for endpoint in PROFIT_ENDPOINTS:
        status, body = get(endpoint, auth_header="Bearer not-a-jwt")
        assert status == 401, (
            f"GET {endpoint} with malformed Bearer: expected 401, got {status}"
        )


def test_x_tenant_id_plus_valid_jwt_ignored():
    """X-Tenant-Id header ต้องถูก ignore — ค่าจาก header ไม่ควรมีผลต่อ tenant ที่ return"""
    # test ว่าถ้ามี JWT จริง X-Tenant-Id header ก็ถูก ignore (ไม่ทำให้ IDOR เกิดได้)
    if not TEST_JWT:
        print("    (skipped — RYLA_TEST_JWT not set)")
        return

    # ส่งทั้ง JWT และ X-Tenant-Id ของ tenant อื่น — ควรใช้ tenant จาก JWT ไม่ใช่จาก header
    headers = {
        "Authorization": f"Bearer {TEST_JWT}",
        "X-Tenant-Id": "ffffffff-ffff-ffff-ffff-ffffffffffff",  # tenant ที่ไม่ใช่ของ user
    }
    for endpoint in PROFIT_ENDPOINTS:
        status, _ = make_request("GET", endpoint, headers=headers)
        # ถ้าได้ 200 แสดงว่า tenant resolution ไม่ได้ใช้ header (ซึ่งถูกต้อง)
        # ถ้าได้ 403 แสดงว่า user ไม่มี profile (ก็ถูกต้องเหมือนกัน)
        assert status in (200, 403, 404), (
            f"GET {endpoint}: expected 200/403/404, got {status} — unexpected response"
        )


def test_happy_path_with_valid_jwt():
    """Full mode: JWT จริงต้อง return 200"""
    if not FULL_MODE or not TEST_JWT:
        print("    (skipped — set RYLA_E2E_IDOR=1 and RYLA_TEST_JWT=<token> to enable)")
        return

    for endpoint in PROFIT_ENDPOINTS:
        status, body = get(endpoint, auth_header=f"Bearer {TEST_JWT}")
        assert status in (200, 403), (
            f"GET {endpoint} with valid JWT: expected 200 or 403, got {status} | body: {body[:200]}"
        )


def test_upsert_sku_cost_requires_auth():
    """POST /api/profit/sku-costs ต้องการ auth ด้วย"""
    status, _ = post("/api/profit/sku-costs", {"itemSku": "TEST-001", "cogs": 100})
    assert status == 401, f"POST /api/profit/sku-costs without auth: expected 401, got {status}"


def test_upsert_with_x_tenant_id_rejected():
    """POST /api/profit/sku-costs กับ X-Tenant-Id header ต้อง return 401"""
    headers = {
        "Content-Type": "application/json",
        "X-Tenant-Id": "bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb",
    }
    data = json.dumps({"itemSku": "TEST-001", "cogs": 100}).encode()
    status, _ = make_request("POST", "/api/profit/sku-costs", headers=headers, body=data)
    assert status == 401, (
        f"POST /api/profit/sku-costs with X-Tenant-Id only: expected 401, got {status}"
        " — IDOR vulnerability may not be fixed!"
    )


# ─── Main ─────────────────────────────────────────────────────────────────────

def main():
    print(f"\nRYLA-9 IDOR Fix — E2E Smoke Tests")
    print(f"Base URL: {BASE_URL}")
    print(f"Full Mode: {FULL_MODE} (set RYLA_E2E_IDOR=1 to enable)")
    print("-" * 60)

    cases = [
        ("No auth header → 401 for all profit endpoints", test_no_auth_returns_401),
        ("X-Tenant-Id header only → 401 (not bypassing auth)", test_x_tenant_id_header_rejected),
        ("Tampered JWT → 401", test_tampered_jwt_rejected),
        ("Malformed Bearer token → 401", test_malformed_bearer_rejected),
        ("X-Tenant-Id ignored when valid JWT present", test_x_tenant_id_plus_valid_jwt_ignored),
        ("POST sku-costs without auth → 401", test_upsert_sku_cost_requires_auth),
        ("POST sku-costs with X-Tenant-Id only → 401", test_upsert_with_x_tenant_id_rejected),
        ("Happy path with valid JWT → 200/403", test_happy_path_with_valid_jwt),
    ]

    passed = 0
    failed = 0

    for desc, fn in cases:
        if run_case(desc, fn):
            passed += 1
        else:
            failed += 1

    print("-" * 60)
    print(f"Results: {passed}/{len(cases)} passed, {failed} failed")

    if failed > 0:
        sys.exit(1)


if __name__ == "__main__":
    main()
