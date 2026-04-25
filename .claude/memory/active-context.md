# Ryla — Active Session Context
<!-- อัพเดทไฟล์นี้หลังทำงานแต่ละ session โดย orchestrator-agent -->

## Current Sprint Focus
<!-- Feature หรือ milestone ที่กำลังทำอยู่ -->
- [ ] [RYLA-5] Google Sheets append-row adapter — implementation done, awaiting E2E + PR (2026-04-20)
- [x] [RYLA-4 + DX-01] LINE OA push + DB fail-fast workflow — merged to develop via PR #7 (2026-04-20)
- [x] [RYLA-3] Shopee webhook receiver — merged to develop via PR #6 (2026-04-20)
- [x] [RYLA-2] TikTok Shop webhook receiver — merged to main, tagged v0.1.0
- [x] [RYLA-1] Tenant Onboarding — merged to main, tagged v0.1.0
- [x] [RYLA-0] Health Check Endpoint — already done in initial setup

## Last 3 Completed Tasks
1. [RYLA-5] Google Sheets append-row adapter — manual JWT RS256 + token cache + fail-soft + 86 tests + 0 AOT warnings (2026-04-20)
2. [RYLA-4 + DX-01] LINE OA push adapter + fail-fast DB workflow — 96 tests pass, 91% coverage, 0 AOT warnings (2026-04-20)
3. [RYLA-3] Shopee webhook receiver — HMAC-SHA256 + replay protection + 9/9 E2E pass (2026-04-20)

## Active Branch
<!-- อัพเดทเมื่อเริ่ม feature ใหม่ -->
- `feat/RYLA-5-google-sheets-adapter` — branched from develop (2026-04-20)

## Latest Release
- `v0.1.0` — Health Check + Tenant Onboarding + TikTok Shop Webhook (2026-04-19)

## Repo Status
- **Visibility:** Public (เปลี่ยนจาก private 2026-04-19)
- **Branch protection:** main + develop require PR + `All Checks Passed` + `Test & Coverage Gate` + `Native AOT Build`
- **CI:** เขียวทุก check รวม security scan (CodeQL, Gitleaks, npm/NuGet audit)

## Known Blockers
- None

## Next Up
- **Real-world validation** — test Shopee HMAC + LINE + Google Sheets API กับ platform sandbox ของจริง (ก่อน v0.2.0)
- **Release v0.2.0** — cut release branch รวม RYLA-3 + RYLA-4 + RYLA-5
- **Flow Configuration UI (RYLA-6)** — dashboard page สำหรับ connect platforms (frontend)
