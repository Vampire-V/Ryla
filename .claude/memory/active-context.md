# Ryla — Active Session Context
<!-- อัพเดทไฟล์นี้หลังทำงานแต่ละ session โดย orchestrator-agent -->

## Current Sprint Focus
<!-- Feature หรือ milestone ที่กำลังทำอยู่ -->
- [x] [RYLA-6] Flow Configuration UI — merged to develop via PR #10 (2026-05-10)
- [x] fix/google-oauth-redirect-and-tenant-quota — merged to develop via PR #11 (2026-05-10)
- [x] [RYLA-5] Google Sheets append-row adapter — merged to develop via PR #8 (2026-04-25)
- [x] [RYLA-4 + DX-01] LINE OA push + DB fail-fast workflow — merged to develop via PR #7 (2026-04-20)
- [x] [RYLA-3] Shopee webhook receiver — merged to develop via PR #6 (2026-04-20)
- [x] [RYLA-2] TikTok Shop webhook receiver — merged to main, tagged v0.1.0
- [x] [RYLA-1] Tenant Onboarding — merged to main, tagged v0.1.0

## Last 3 Completed Tasks
1. **PR #11 merged** — tenant quota migration, Google OAuth redirect_uri fix, gitignore improvements (2026-05-10)
2. **RYLA-6 full E2E validated** — Google OAuth login ✅ + LINE OA connect ✅ + test notification ✅ (2026-05-10)
3. **PR #10 merged** — RYLA-6 Flow Configuration UI (dashboard, connections CRUD, LINE/Sheets forms, webhook page, test notification endpoint) (2026-05-10)

## Active Branch
- `develop` — ไม่มี feature branch ที่ active อยู่ในขณะนี้

## Latest Release
- `v0.2.0` — Shopee webhook + LINE OA push + Google Sheets adapter (2026-04-25)
- develop มี RYLA-6 UI + tenant quota + OAuth fixes (ยังไม่ release เป็น v0.3.0)

## Repo Status
- **Visibility:** Public
- **Branch protection:** main + develop require PR + `All Checks Passed` + `Test & Coverage Gate` + `Native AOT Build`
- **CI:** เขียวทุก check รวม security scan (CodeQL, Gitleaks, npm/NuGet audit)

## Known Blockers
- None

## Next Up
- **RYLA-7: Azure Functions deployment** — infrastructure setup
- **v0.3.0 release** — compile RYLA-6 + PR #10/#11 into release
- **Shopee / TikTok real validation** — defer (ต้องมี business partner account)
