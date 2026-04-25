# RYLA-4 — LINE OA Push Notification Adapter (Design ADR)

**Status:** Implemented (2026-04-20)
**Branch:** `feat/RYLA-4-line-push-adapter`
**Depends on:** RYLA-1 (Tenant Onboarding), RYLA-2 (TikTok webhook), RYLA-3 (Shopee webhook)

---

## Context

Ryla ใช้ inbound webhooks จาก TikTok Shop (RYLA-2) และ Shopee (RYLA-3) รับ order events
แต่หลัง HMAC verify endpoints แค่ log + return 200 — ยังไม่ส่ง value ให้ merchant

RYLA-4 ส่งมอบ **value proposition หลักของ Ryla**: merchant ได้รับ LINE notification ทันทีเมื่อมี order
ผ่าน LINE Messaging API push message

---

## Architecture

```
TikTok/Shopee Webhook (POST /webhooks/{platform})
    │
    ▼ HMAC verify + parse
    ├── (401) invalid signature → return
    ├── (422) malformed JSON → return
    │
    ▼ OrderWebhookContext { Platform, ShopId, OrderId, EventType }
    │
    ▼ IProcessOrderWebhookUseCase
    │   ├─ IConnectionRepository.FindTenantIdByShopIdAsync(platform, shopId)
    │   │     └── NULL → SkippedNoTenant
    │   ├─ IConnectionRepository.GetLineCredentialsAsync(tenantId)
    │   │     └── NULL → SkippedNoLineConfig
    │   ├─ FormatMessage: "🔔 {Platform}: Order #{OrderId} — {EventType}"
    │   └─ ILineNotifier.PushAsync(token, userId, message)
    │         ├── Success=true  → Sent
    │         └── Success=false → FailedLineDelivery (swallowed, log only)
    │
    ▼ always 200 (endpoint wraps use case in try/catch)
```

**Layers:**
- `Ryla.Core` — port interfaces, domain records, use case (pure, zero external deps, AOT-strict)
- `Ryla.Infrastructure` — `ConnectionRepository` (Npgsql), `LineMessagingClient` (HttpClient)
- `Ryla.Api` — endpoints wire context → use case, swallow exceptions → always 200

---

## Architectural Decisions

### ADR-1: ใช้ existing `connections` table แทน columns บน `tenants`

**Decision:** จัดเก็บ per-tenant LINE credentials + platform shop_id mapping ใน `connections(tenant_id, platform, credentials jsonb, is_active)` ที่มีใน init migration

**Context:** ตอนแรก plan เสนอเพิ่ม 4 columns บน `tenants` (line_channel_access_token, line_target_user_id, tiktok_shop_id, shopee_shop_id) เหตุผลหลักคือหลีกเลี่ยง AOT jsonb parsing tax และ RLS complexity

**Alternative considered:** เพิ่ม columns บน `tenants`

**ทำไมเลือก connections:**
- Table มีอยู่แล้วใน schema ตั้งแต่ initial migration (`20260407000000_init.sql`) — ไม่ใช่ premature abstraction เพราะมาก่อน requirement
- CHECK constraint รองรับ 4 platforms ที่ Ryla ตั้งใจ: `tiktok_shop`, `shopee`, `line_oa`, `google_sheets` — aligned กับ roadmap
- RLS policy `"connections: tenant isolation"` + indexes `idx_connections_tenant`, `idx_connections_platform` มีแล้ว
- AOT cost ต่ำกว่าที่กลัว: Postgres ทำ `credentials->>'key'` projection ให้เป็น `text` ก่อนส่งกลับ — Npgsql read แบบ `GetString()` ธรรมดา ไม่ต้องใช้ `JsonSerializerContext` สำหรับ dynamic keys
- Schema evolution ฟรี: Google Sheets (RYLA-5) และ platforms ถัดไปใช้ table เดียวกัน โดยใส่ credentials ที่ต่างกันใน jsonb

**ทำไมไม่เลือก columns on tenants:**
- Duplicate storage pattern (table อีกอันทำงานเดียวกันอยู่แล้ว)
- ต้อง migrate อีกครั้งตอน RYLA-5 เมื่อมี Google Sheets → generalize ไป jsonb ทีหลัง
- แตะ tenants table = touch อันใจกลางของ multi-tenant model (higher risk)

**Migration ที่เพิ่ม:** partial functional index สำหรับ reverse lookup (shop_id → tenant)
```sql
CREATE INDEX idx_connections_platform_shop_id
  ON public.connections (platform, (credentials->>'shop_id'))
  WHERE is_active = true AND credentials ? 'shop_id';
```

### ADR-2: Webhook **คืน 200 เสมอ**

**Decision:** ทุก failure mode ของ use case (DB down, LINE 4xx/5xx, timeout, tenant not found) → HTTP 200 + log error

**Why:**
- TikTok/Shopee retry webhooks ที่ได้ 5xx → retry storm ถ้าฝั่งเรามีปัญหายาวนาน
- 4xx/5xx ไม่ช่วย merchant เพราะเราเป็นคนผิด — platform ทำอะไรไม่ได้
- Observability: log + metric เป็น signal สำหรับ alerting (ภายหลัง) ดีกว่าส่ง status code ผิดไปให้ platform

**Implementation:** `TikTokWebhookEndpoints.ProcessOrderEventAsync` + `ShopeeWebhookEndpoints.ProcessOrderEventAsync` wrap use case call ใน try/catch → log เท่านั้น ไม่ propagate

**Trade-off:** ถ้า DB ล่มยาว → LINE notification หลุด แต่ไม่กระทบ webhook inbox (platform เห็นเรา healthy) — acceptable เพราะ platform จะเก็บ webhook history ไว้ ให้เรา replay ได้ภายหลัง (future work)

### ADR-3: Per-call token injection ใน `ILineNotifier`

**Decision:** `PushAsync(channelAccessToken, targetUserId, message, ct)` รับ token/userId เป็น parameter ทุก call ไม่ใช่ inject ผ่าน `IOptions<LineOptions>`

**Why:** token + userId เป็น **per-tenant** ไม่ใช่ application config. การ inject ผ่าน IOptions จะทำให้ adapter ผูกกับ 1 tenant — ผิด multi-tenant model

`LineOptions` ยังมีอยู่แต่เก็บเฉพาะ `BaseUrl` (global, เปลี่ยนใน E2E stub mode)

### ADR-4: Message format minimal v1

**Decision:** `"🔔 {PlatformDisplayName}: Order #{OrderId} — {EventType}"`

**Examples:**
- `"🔔 TikTok Shop: Order #E2E_TIKTOK_001 — ORDER_STATUS_CHANGE"`
- `"🔔 Shopee: Order #E2E_SHOPEE_001 — 3"` (code 3 = order event)

**Why minimal:**
- Order detail (amount, product, buyer) ไม่อยู่ใน webhook payload — ต้อง call platform API เพิ่ม = chain complexity + latency
- ปล่อยให้ merchant decide format ที่ต้องการ → roadmap: template system
- ภาษาไทยโดย default (emoji + structure) → merchant audience

**Defer:** localization, merchant-customizable template, รวบ orders รัว → single summary

### ADR-5: ไม่สร้าง event-driven pipeline

**Decision:** Use case เรียก adapter **โดยตรง** ใน synchronous flow. ไม่ทำ `IEventPublisher` + message bus

**Why:** 1 adapter = over-engineering. Revisit เมื่อ:
- มี adapter ที่ 2 ที่ต้อง parallelize (Google Sheets + LINE)
- ต้อง retry/DLQ สำหรับ failed notifications
- Throughput ต้องการ decouple webhook receive จาก notification send

---

## Testing

### Unit tests (23 Core, 16 Api, 25 Infra = 64 total, +15 from RYLA-3)

**Core:**
- `ProcessOrderWebhookUseCaseTests` (6 cases) — mock `IConnectionRepository` + `ILineNotifier` ด้วย NSubstitute
- `ShopeeHmacVerifierTests` + `TikTokHmacVerifierTests` เดิม

**Infrastructure:**
- `ConnectionRepositoryTests` (6 cases) — Testcontainers Postgres + seed via raw SQL
- `LineMessagingClientTests` (3 cases) — fake `HttpMessageHandler`
- `TenantRepositoryTests` เดิม

**Api:**
- `ShopeeWebhookEndpointsTests` + `TikTokWebhookEndpointsTests` — updated mock setup (inject `IProcessOrderWebhookUseCase`)

### E2E tests (`tests/e2e/line_notification.py`)

**Regression mode (default):** 4 cases
- TikTok + Shopee webhooks ยังคืน 200 หลัง LINE wiring (wiring ไม่ทำ HTTP contract ผิด)
- Invalid HMAC ยังคืน 401 (use case ไม่ถูกเรียก)

**Full mode (RYLA_E2E_LINE=1):** +4 cases
- LINE stub รับ push เมื่อ tenant + connection configured
- Push body มี platform + order_id
- LINE 401 → webhook ยังคืน 200

**Regression total:** 22 E2E tests pass (4 RYLA-4 regression + 9 Shopee + 9 TikTok)

---

## Files

**New (core):**
- `backend/src/Ryla.Core/Configuration/LineOptions.cs`
- `backend/src/Ryla.Core/Domain/Integrations/LineCredentials.cs`
- `backend/src/Ryla.Core/Domain/Webhooks/Platform.cs`
- `backend/src/Ryla.Core/Domain/Webhooks/OrderWebhookContext.cs`
- `backend/src/Ryla.Core/Domain/Webhooks/TikTokOrderContent.cs`
- `backend/src/Ryla.Core/Ports/Outbound/IConnectionRepository.cs`
- `backend/src/Ryla.Core/Ports/Outbound/ILineNotifier.cs`
- `backend/src/Ryla.Core/UseCases/IProcessOrderWebhookUseCase.cs`
- `backend/src/Ryla.Core/UseCases/ProcessOrderWebhookUseCase.cs`
- `backend/src/Ryla.Core/UseCases/CoreServiceCollectionExtensions.cs`

**New (infra):**
- `backend/src/Ryla.Infrastructure/Adapters/Connections/ConnectionRepository.cs`
- `backend/src/Ryla.Infrastructure/Adapters/LineMessaging/LineJsonContext.cs`
- `backend/src/Ryla.Infrastructure/Adapters/LineMessaging/LineMessagingClient.cs`

**New (tests):**
- `backend/tests/Ryla.Core.Tests/UseCases/ProcessOrderWebhookUseCaseTests.cs`
- `backend/tests/Ryla.Infrastructure.Tests/Adapters/ConnectionRepositoryTests.cs`
- `backend/tests/Ryla.Infrastructure.Tests/Adapters/LineMessagingClientTests.cs`
- `tests/e2e/line_notification.py`
- `tests/e2e/helpers/line_stub_server.py`
- `tests/e2e/fixtures/line_notification_seed.sql`

**New (migration):**
- `supabase/migrations/20260420000000_add_connection_shop_id_index.sql`

**Modified:**
- `backend/src/Ryla.Api/Endpoints/TikTokWebhookEndpoints.cs` — wire use case + try/catch
- `backend/src/Ryla.Api/Endpoints/ShopeeWebhookEndpoints.cs` — wire use case + try/catch
- `backend/src/Ryla.Api/Extensions/ServiceCollectionExtensions.cs` — DI + JSON context
- `backend/src/Ryla.Api/appsettings.json` — Line.BaseUrl default
- `backend/src/Ryla.Core/Ryla.Core.csproj` — ILogger abstractions
- `backend/src/Ryla.Infrastructure/Ryla.Infrastructure.csproj` — HttpClient factory
- `backend/src/Ryla.Infrastructure/DependencyInjection.cs` — register ConnectionRepo + LineClient

---

## Known limitations / Follow-ups

1. **LINE scheme ยังไม่ได้ validate กับ LINE production** — stub ที่ใช้ใน E2E ทำ contract check เท่านั้น ต้อง integration test กับ LINE OA channel ตัวจริง (RYLA-5 pre-reqs)
2. **Shopee scheme ยังไม่ได้ validate กับ Shopee sandbox** — carry-over จาก RYLA-3
3. **Retry / DLQ** ไม่มี — ถ้า LINE/DB ล่ม notification หลุด (mitigation: platform webhook replay ภายหลัง)
4. **Rate limit** — LINE Messaging free tier 500 msg/month per channel — ไม่ enforce ฝั่งเรา (log only)
5. **Order detail fetch** — message minimal ไม่ดึง amount/product (future: call TikTok/Shopee REST API)
6. **Template customization** — merchant ไม่สามารถ customize format ได้ (future: per-tenant template)
