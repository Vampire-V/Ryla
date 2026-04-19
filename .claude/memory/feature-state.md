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
- **[RYLA-2] TikTok Shop Webhook Receiver** — HMAC-SHA256 + replay protection + 9/9 E2E — 2026-04-19
- **[RYLA-3] Shopee Webhook Receiver** — HMAC-SHA256 + replay protection + 9/9 E2E — PR #6 — 2026-04-20
- **[RYLA-4 + DX-01] LINE OA Push Adapter + Fail-Fast DB workflow** — 96 tests, 91% coverage, 0 AOT warnings — PR #7 — 2026-04-20
- **[RYLA-5] Google Sheets Append-Row Adapter** — manual JWT (RS256) + token cache + fail-soft + 86 unit/integration tests + 0 AOT warnings — feat/RYLA-5-google-sheets-adapter — 2026-04-20

---

## Backlog (Priority Order)

1. **Real-world platform validation** — test HMAC + LINE + Sheets API against real sandboxes (ก่อน v0.2.0)
2. **Release v0.2.0** — cut release branch รวม RYLA-3 + RYLA-4 + RYLA-5
3. **Flow Configuration UI (RYLA-6)** — dashboard page for connecting platforms
4. **End-to-end pipe wiring polish** — observability metrics + retry policy
