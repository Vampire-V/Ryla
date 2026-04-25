# Ryla — Active Session Context
<!-- อัพเดทไฟล์นี้หลังทำงานแต่ละ session โดย orchestrator-agent -->

## Current Sprint Focus
<!-- Feature หรือ milestone ที่กำลังทำอยู่ -->
- [ ] [RYLA-6] Flow Configuration UI — branch `feat/RYLA-5-google-sheets-adapter` (implementation complete, awaiting PR) (2026-04-25)
- [x] [RYLA-5] Google Sheets append-row adapter — merged to develop via PR #8 (2026-04-25)
- [x] [RYLA-4 + DX-01] LINE OA push + DB fail-fast workflow — merged to develop via PR #7 (2026-04-20)
- [x] [RYLA-3] Shopee webhook receiver — merged to develop via PR #6 (2026-04-20)
- [x] [RYLA-2] TikTok Shop webhook receiver — merged to main, tagged v0.1.0
- [x] [RYLA-1] Tenant Onboarding — merged to main, tagged v0.1.0

## Last 3 Completed Tasks
1. [RYLA-6] Flow Configuration UI — 15 tasks implemented (Google OAuth, dashboard, connections CRUD, LINE/Sheets forms, webhook page, test notification endpoint, E2E script) (2026-04-25)
2. **Real-world validation** — LINE push ✅ + Google Sheets append ✅ ทำงานกับ platform จริง (2026-04-25)
3. **v0.2.0 released** — RYLA-3 + RYLA-4 + RYLA-5 merged, tagged + GitHub Release created (2026-04-25)

## Active Branch
- `feat/RYLA-5-google-sheets-adapter` (NOTE: branch name predates RYLA-6 — contains RYLA-6 implementation) — awaiting human E2E verification + PR

## Latest Release
- `v0.2.0` — Shopee webhook + LINE OA push + Google Sheets adapter (2026-04-25)

## Repo Status
- **Visibility:** Public
- **Branch protection:** main + develop require PR + `All Checks Passed` + `Test & Coverage Gate` + `Native AOT Build`
- **CI:** เขียวทุก check รวม security scan (CodeQL, Gitleaks, npm/NuGet audit)

## Known Blockers
- None

## Next Up
- **Create PR for RYLA-6** — human must run E2E first: restart backend + `python3 tests/e2e/test_notification_api.py` → then `make test-e2e`
- **Azure Functions deployment (RYLA-7)** — infrastructure setup
- **Shopee / TikTok real validation** — defer (ต้องมี business partner account)
