#!/usr/bin/env python3
"""E2E smoke tests for Ryla landing page redesign."""
import sys
import urllib.request
import urllib.error

BASE = "http://localhost:3000"

passed = 0
failed = 0


def ok(label: str) -> None:
    global passed
    passed += 1
    print(f"  [PASS] {label}")


def fail(label: str, detail: str = "") -> None:
    global failed
    failed += 1
    suffix = f": {detail}" if detail else ""
    print(f"  [FAIL] {label}{suffix}")


def get(path: str) -> tuple[int, str]:
    try:
        with urllib.request.urlopen(f"{BASE}{path}", timeout=10) as r:
            return r.status, r.read().decode("utf-8", errors="replace")
    except urllib.error.HTTPError as e:
        return e.code, ""
    except urllib.error.URLError as e:
        print(f"  [ERROR] Cannot reach {BASE}: {e}")
        return 0, ""


def main() -> None:
    print("\nLanding Page — Redesign Smoke Tests")
    print("=" * 50)

    status, html = get("/")

    # 1. Root page returns 200 (no redirect to login)
    if status == 200:
        ok("GET / returns 200")
    else:
        fail("GET / returns 200", f"got {status}")

    # 2. Hero heading present
    if "ออเดอร์เข้า" in html and "LINE" in html:
        ok("Hero heading present ('ออเดอร์เข้า LINE')")
    else:
        fail("Hero heading present", "missing 'ออเดอร์เข้า' or 'LINE'")

    # 3. CTA links
    if "/auth/signup" in html:
        ok("CTA /auth/signup present")
    else:
        fail("CTA /auth/signup present")

    if "/auth/login" in html:
        ok("CTA /auth/login present")
    else:
        fail("CTA /auth/login present")

    # 4. No replaced emoji in source
    for emoji in ["⚡", "📊", "💬", "✏️", "🗑️"]:
        if emoji not in html:
            ok(f"No '{emoji}' emoji in source")
        else:
            fail(f"No '{emoji}' emoji in source", "found in HTML")

    # 5. Section headings
    for keyword, section in [("ขั้นตอน", "Section 2"), ("Ryla", "Section 3"), ("พร้อม", "Section 4")]:
        if keyword in html:
            ok(f"{section} heading present ('{keyword}')")
        else:
            fail(f"{section} heading present", f"missing '{keyword}'")

    print(f"\nResult: {passed}/{passed + failed} passed")
    if failed:
        sys.exit(1)


if __name__ == "__main__":
    main()
