# Ryla — Active Session Context
<!-- อัพเดทไฟล์นี้หลังทำงานแต่ละ session โดย orchestrator-agent -->

## Current Sprint Focus
<!-- Feature หรือ milestone ที่กำลังทำอยู่ -->
- [x] [RYLA-2] TikTok Shop webhook receiver — merged to main, tagged v0.1.0
- [x] [RYLA-1] Tenant Onboarding — merged to main, tagged v0.1.0
- [x] [RYLA-0] Health Check Endpoint — already done in initial setup

## Last 3 Completed Tasks
1. [RYLA-2] TikTok Shop webhook receiver — HMAC-SHA256 + replay protection + 9/9 E2E pass (2026-04-19)
2. [RYLA-1] Tenant Onboarding — PostgreSQL trigger + ITenantRepository + 12 integration tests (2026-04-16)
3. AI Orchestration Layer setup — agents + hooks + Makefile + memory (2026-04-07)

## Active Branch
<!-- อัพเดทเมื่อเริ่ม feature ใหม่ -->
- `develop` — integration branch (Gitflow)

## Latest Release
- `v0.1.0` — Health Check + Tenant Onboarding + TikTok Shop Webhook (2026-04-19)

## Known Blockers
- None

## Next Up
- **Shopee webhook receiver** — HMAC verify + order status change
- **LINE OA push notification adapter** — outbound LINE Messaging API
