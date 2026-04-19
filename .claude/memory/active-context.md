# Ryla — Active Session Context
<!-- อัพเดทไฟล์นี้หลังทำงานแต่ละ session โดย orchestrator-agent -->

## Current Sprint Focus
<!-- Feature หรือ milestone ที่กำลังทำอยู่ -->
- [ ] [RYLA-4] LINE OA push notification adapter — in progress on `feat/RYLA-4-line-push-adapter`
- [x] [RYLA-3] Shopee webhook receiver — merged to develop via PR #6 (2026-04-20)
- [x] [RYLA-2] TikTok Shop webhook receiver — merged to main, tagged v0.1.0
- [x] [RYLA-1] Tenant Onboarding — merged to main, tagged v0.1.0
- [x] [RYLA-0] Health Check Endpoint — already done in initial setup

## Last 3 Completed Tasks
1. [RYLA-3] Shopee webhook receiver — HMAC-SHA256 + replay protection + 9/9 E2E pass (2026-04-20)
2. [RYLA-2] TikTok Shop webhook receiver — HMAC-SHA256 + replay protection + 9/9 E2E pass (2026-04-19)
3. [RYLA-1] Tenant Onboarding — PostgreSQL trigger + ITenantRepository + 12 integration tests (2026-04-16)

## Active Branch
<!-- อัพเดทเมื่อเริ่ม feature ใหม่ -->
- `feat/RYLA-4-line-push-adapter` — branched from develop (2026-04-20)

## Latest Release
- `v0.1.0` — Health Check + Tenant Onboarding + TikTok Shop Webhook (2026-04-19)

## Repo Status
- **Visibility:** Public (เปลี่ยนจาก private 2026-04-19)
- **Branch protection:** main + develop require PR + `All Checks Passed` + `Test & Coverage Gate` + `Native AOT Build`
- **CI:** เขียวทุก check รวม security scan (CodeQL, Gitleaks, npm/NuGet audit)

## Known Blockers
- None

## Next Up
- **LINE OA push notification adapter** — outbound LINE Messaging API
- **Google Sheets adapter** — append order data จาก webhook ไปชีท
- **Shopee real-world validation** — test HMAC scheme กับ Shopee sandbox (signature scheme ยังไม่ได้ verify กับ platform จริง)
