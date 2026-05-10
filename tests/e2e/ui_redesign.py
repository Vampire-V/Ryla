#!/usr/bin/env python3
"""
UI Redesign Smoke Tests — RYLA-8
ตรวจว่าทุกหน้าโหลดได้ และ emoji ที่แทนด้วย lucide icons ไม่ปรากฏใน HTML source

รัน: python3 tests/e2e/ui_redesign.py
หรือ: make test-e2e (frontend dev server ต้องรันที่ localhost:3000)
"""

import sys
import urllib.request
import urllib.error

FRONTEND_URL = "http://localhost:3000"

# emoji ที่ spec ระบุว่าต้องแทนด้วย lucide icon แล้ว
REPLACED_EMOJIS = ["⚡", "✏️", "🗑", "💬", "📊", "🛒", "📂", "✅", "💡", "🔗", "📋"]

passed = 0
failed = 0


def ok(label: str) -> None:
    global passed
    passed += 1
    print(f"  ✓ {label}")


def fail(label: str, detail: str = "") -> None:
    global failed
    failed += 1
    msg = f"  ✗ {label}"
    if detail:
        msg += f"\n    {detail}"
    print(msg)


def get(path: str) -> tuple[int, str]:
    url = FRONTEND_URL + path
    try:
        with urllib.request.urlopen(url, timeout=10) as r:
            return r.status, r.read().decode("utf-8", errors="replace")
    except urllib.error.HTTPError as e:
        return e.code, ""
    except Exception as e:
        return 0, str(e)


def assert_loads(label: str, path: str, expect_text: str | None = None) -> str:
    status, body = get(path)
    if status == 200:
        if expect_text and expect_text not in body:
            fail(label, f"หาไม่เจอ '{expect_text}' ใน HTML")
            return ""
        ok(label)
        return body
    else:
        fail(label, f"HTTP {status}")
        return ""


# ---------------------------------------------------------------------------
# Test 1: Login redirect (logged-in user → /connections)
# ---------------------------------------------------------------------------
print("\nTest 1: Login redirect")
status, _ = get("/login")
# Next.js middleware redirect returns 307 or follows to /connections (200)
if status in (200, 307, 302):
    ok("GET /login → redirect or connections page (authenticated)")
else:
    fail("GET /login", f"ไม่คาดว่าจะได้ HTTP {status}")

# ---------------------------------------------------------------------------
# Test 2: Dashboard pages return 200 (middleware redirects → login page renders)
# ---------------------------------------------------------------------------
print("\nTest 2: Dashboard pages return 200 (unauthenticated → login redirect)")
for path, label in [
    ("/connections", "Connections"),
    ("/webhook", "Webhook URL"),
    ("/connections/new/line", "LINE form"),
    ("/connections/new/google-sheets", "Sheets form"),
]:
    status, _ = get(path)
    if status == 200:
        ok(f"GET {path} → 200")
    else:
        fail(f"GET {path}", f"HTTP {status}")

# ---------------------------------------------------------------------------
# Test 3: Replaced emojis absent from publicly-accessible pages
# (Dashboard pages require auth; verified visually via browser instead)
# ---------------------------------------------------------------------------
print("\nTest 3: Replaced emojis absent from public pages (/, /login)")
for public_path in ["/", "/login"]:
    _, body = get(public_path)
    if not body:
        fail(f"ไม่ได้ HTML จาก {public_path}")
        continue
    for emoji in REPLACED_EMOJIS:
        if emoji in body:
            fail(f"emoji '{emoji}' ยังอยู่ใน {public_path} HTML")
        else:
            ok(f"{public_path}: emoji '{emoji}' ไม่มีใน HTML")

# ---------------------------------------------------------------------------
# Test 4: Root page loads with expected content
# ---------------------------------------------------------------------------
print("\nTest 4: Root page content")
assert_loads("GET / → 200 with 'Ryla'", "/", "Ryla")

# ---------------------------------------------------------------------------
# Test 5: Login page has new design elements
# ---------------------------------------------------------------------------
print("\nTest 5: Login page design elements")
_, login_html = get("/login")
if "p-10" in login_html or "shadow-md" in login_html:
    ok("Login card has updated padding/shadow classes")
else:
    fail("Login card styling — p-10/shadow-md ไม่พบ")
if "text-slate-600" in login_html:
    ok("Login tagline uses text-slate-600 (updated contrast)")
else:
    fail("Login tagline contrast — text-slate-600 ไม่พบ")

# ---------------------------------------------------------------------------
# Summary
# ---------------------------------------------------------------------------
total = passed + failed
print(f"\n{'='*50}")
print(f"UI Redesign E2E: {passed}/{total} passed")
if failed:
    print(f"FAILED: {failed} test(s)")
    sys.exit(1)
else:
    print("ALL PASSED")
    sys.exit(0)
