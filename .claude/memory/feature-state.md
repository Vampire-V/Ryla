# Feature Implementation State — Ryla
<!-- orchestrator-agent อัพเดทหลังแต่ละ feature milestone -->

---

## In Progress
<!-- หนึ่ง entry ต่อ active feature branch -->

_(ยังไม่มี feature ที่กำลัง implement)_

---

## Template สำหรับ In-Progress Feature

```markdown
### [RYLA-XXX] Feature Name
- **Branch:** feat/RYLA-XXX-feature-slug
- **Phase:** 1-Design / 2-Tests / 3-Backend / 4-Frontend / 5-AOT / 6-Review / 7-PR
- **Architect Plan:** [reference to ADR or inline summary]
- **Test Files:**
  - `backend/tests/Ryla.Core.Tests/UseCases/XxxUseCaseTests.cs`
  - `backend/tests/Ryla.Api.Tests/Endpoints/XxxEndpointsTests.cs`
- **New AOT types to register in RylaJsonContext:**
  - `XxxRequest`
  - `XxxResponse`
- **New Supabase tables:** (list or N/A)
- **Blockers:** None
```

---

## Completed

- AI Orchestration Layer (agents + hooks + Makefile + memory) — 2026-04-07
- **[RYLA-0] Health Check Endpoint** — `GET /health` AOT-safe, 3 tests — initial setup
- **[RYLA-1] Tenant Onboarding** — PostgreSQL trigger auto-provision tenant+profile on signup — PR #1 — 2026-04-16
- **[RYLA-2] TikTok Shop Webhook Receiver** — HMAC-SHA256 + replay protection + 9/9 E2E — branch feat/RYLA-2-tiktok-webhook — 2026-04-19

---

## Backlog (Priority Order)

1. **Shopee webhook receiver** — HMAC verify + order status change
2. **LINE OA push notification adapter** — outbound LINE Messaging API
3. **Google Sheets append row adapter** — outbound Google Sheets API
4. **Flow configuration UI** — dashboard page for connecting platforms
