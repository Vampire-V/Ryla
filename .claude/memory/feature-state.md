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

---

## Backlog (Priority Order)

1. **Health check endpoint** — `GET /health` → `{ status, timestamp }` (no auth required)
2. **Tenant onboarding** — create tenant + profile via Supabase Auth webhook
3. **TikTok Shop webhook receiver** — HMAC verify + order event processing
4. **Shopee webhook receiver** — HMAC verify + order status change
5. **LINE OA push notification adapter** — outbound LINE Messaging API
6. **Google Sheets append row adapter** — outbound Google Sheets API
7. **Flow configuration UI** — dashboard page for connecting platforms
