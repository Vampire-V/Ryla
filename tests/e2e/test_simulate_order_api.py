#!/usr/bin/env python3
"""
E2E smoke tests for POST /api/simulate-order

Tests (in order):
  1. Missing secret → 401
  2. Wrong secret → 401
  3. Invalid tenantId format → 400
  4. Valid tenant with LINE connection → 200 + orderId has SIM- prefix
     (lineSuccess=true only asserted with RYLA_E2E_LINE=1)
  5. Unknown tenant (no connections) → 200 + lineSuccess=false

Pre-requisites:
  - App running at localhost:5282 (make dev && dotnet run)
  - Seed tenant 11111111-1111-1111-1111-111111111111 with line_oa connection in local DB
  - RYLA_E2E_LINE=1 to assert actual LINE delivery success (requires real LINE token)
"""

import json
import os
import sys
import urllib.error
import urllib.request

BASE_URL = "http://localhost:5282"
ENDPOINT = f"{BASE_URL}/api/simulate-order"
VALID_TENANT_ID = "11111111-1111-1111-1111-111111111111"
UNKNOWN_TENANT_ID = "99999999-9999-9999-9999-999999999999"
INTERNAL_SECRET = "ryla-dev-internal-secret"


def post(payload: dict, secret: str | None) -> tuple[int, dict]:
    body = json.dumps(payload).encode()
    headers = {"Content-Type": "application/json"}
    if secret is not None:
        headers["X-Internal-Secret"] = secret
    req = urllib.request.Request(ENDPOINT, data=body, headers=headers, method="POST")
    try:
        with urllib.request.urlopen(req) as resp:
            return resp.status, json.loads(resp.read())
    except urllib.error.HTTPError as e:
        return e.code, {}


def run_tests() -> bool:
    results: list[tuple[bool, str]] = []

    def test(name: str, ok: bool, detail: str = "") -> None:
        results.append((ok, f"{name}{': ' + detail if detail else ''}"))
        if not ok:
            print(f"  FAIL: {name} — {detail}", file=sys.stderr)

    # 1. Missing secret → 401
    code, _ = post({"tenantId": VALID_TENANT_ID, "platform": "tiktok_shop"}, secret=None)
    test("missing secret → 401", code == 401, f"got {code}")

    # 2. Wrong secret → 401
    code, _ = post({"tenantId": VALID_TENANT_ID, "platform": "tiktok_shop"}, secret="wrong-secret")
    test("wrong secret → 401", code == 401, f"got {code}")

    # 3. Invalid tenantId format → 400
    code, _ = post({"tenantId": "not-a-uuid", "platform": "tiktok_shop"}, secret=INTERNAL_SECRET)
    test("invalid tenantId → 400", code == 400, f"got {code}")

    # 4. Valid tenant → 200 + SIM- orderId (lineSuccess only asserted with RYLA_E2E_LINE=1)
    code, body = post({"tenantId": VALID_TENANT_ID, "platform": "tiktok_shop"}, secret=INTERNAL_SECRET)
    test("valid tenant → 200", code == 200, f"got {code}")
    order_id: str = body.get("orderId", "")
    test("orderId has SIM- prefix", order_id.startswith("SIM-"), f"orderId={order_id}")
    if os.environ.get("RYLA_E2E_LINE") == "1":
        test("lineSuccess=true (real LINE delivery)", body.get("lineSuccess") is True, str(body))

    # 5. Unknown tenant → 200 + lineSuccess=false (no connections)
    code, body = post({"tenantId": UNKNOWN_TENANT_ID, "platform": "tiktok_shop"}, secret=INTERNAL_SECRET)
    test("unknown tenant → 200 (graceful)", code == 200, f"got {code}")
    test("lineSuccess=false for unknown tenant", body.get("lineSuccess") is False, str(body))

    passed = sum(1 for ok, _ in results if ok)
    print(f"\nResults: {passed}/{len(results)} passed")
    for ok, name in results:
        print(f"  [{'PASS' if ok else 'FAIL'}] {name}")

    return passed == len(results)


if __name__ == "__main__":
    ok = run_tests()
    sys.exit(0 if ok else 1)
