# Ryla — Active Session Context
<!-- อัพเดทไฟล์นี้หลังทำงานแต่ละ session โดย orchestrator-agent -->

## Current Sprint Focus
<!-- Feature หรือ milestone ที่กำลังทำอยู่ -->
- [x] pnpm migration + Node 24 upgrade — merged to develop via PR #17 (2026-05-17)
- [x] [RYLA-9] Real Profit Dashboard — merged to develop via PR #16 (2026-05-16)
- [x] Landing page redesign — hero mockup + 4 sections — merged to develop via PR #14 (2026-05-11)
- [x] [RYLA-8] UI Redesign — Calm Authority design language — merged to develop via PR #13 (2026-05-10)
- [x] [RYLA-7] Order Simulator — merged to develop via PR #12 (2026-05-10)
- [x] [RYLA-6] Flow Configuration UI — merged to develop via PR #10 (2026-05-10)
- [x] fix/google-oauth-redirect-and-tenant-quota — merged to develop via PR #11 (2026-05-10)
- [x] [RYLA-5] Google Sheets append-row adapter — merged to develop via PR #8 (2026-04-25)
- [x] [RYLA-4 + DX-01] LINE OA push + DB fail-fast workflow — merged to develop via PR #7 (2026-04-20)
- [x] [RYLA-3] Shopee webhook receiver — merged to develop via PR #6 (2026-04-20)
- [x] [RYLA-2] TikTok Shop webhook receiver — merged to main, tagged v0.1.0
- [x] [RYLA-1] Tenant Onboarding — merged to main, tagged v0.1.0

## Last 3 Completed Tasks
1. **PR #17 merged** — pnpm migration + Node 24: lockfile, Makefile, CI workflows (2026-05-17)
2. **PR #16 merged** — RYLA-9 Real Profit Dashboard: Shopee OAuth, orders/margin analytics, COGS, Azure Functions timer, IDOR fix, 190 tests (2026-05-16)
3. **PR #14 merged** — Landing page redesign: hero mockup, 4 sections, LINE notification animation (2026-05-11)
3. **PR #13 merged** — RYLA-8 UI Redesign: Calm Authority design language, lucide-react icons, sidebar/typography overhaul (2026-05-10)

## Active Branch
- `develop` — ทุก feature ถึง RYLA-9 merge แล้ว พร้อม release v0.3.0

## Latest Release
- `v0.2.0` — Shopee webhook + LINE OA push + Google Sheets adapter (2026-04-25)
- develop มี RYLA-6 UI + tenant quota + OAuth fixes + Order Simulator (ยังไม่ release เป็น v0.3.0)

## Repo Status
- **Visibility:** Public
- **Branch protection:** main + develop require PR + `All Checks Passed` + `Test & Coverage Gate` + `Native AOT Build`
- **CI:** เขียวทุก check รวม security scan (CodeQL, Gitleaks, npm/NuGet audit)

## Known Blockers
- None

## Next Up
- **v0.3.0 release** — compile RYLA-6 + RYLA-7 + RYLA-8 + landing page + RYLA-9 (PR #10-#16) into release
- **Azure Functions deployment** — infrastructure setup สำหรับ SyncOrdersTimerFunction
- **Shopee / TikTok real validation** — defer (ต้องมี business partner account)
