# AGENTS.md — Ryla AI Team Handbook

> **IMPORTANT:** ไฟล์นี้เขียนและดูแลโดย developer เท่านั้น — agents ห้ามแก้ไขโดยตรง
> อัพเดทหลัง session ที่มี learning ใหม่เท่านั้น

---

## Team Roster

| Agent | บทบาท | เรียกใช้เมื่อ |
|---|---|---|
| `orchestrator-agent` | Entry point — routing meta-agent | Request ไม่ชัดเจน, งานข้ามหลาย layer |
| `architect-agent` | System design + AOT compliance | ก่อน implement ทุก feature ใหม่ (mandatory) |
| `backend-engineer-agent` | .NET 10 + AOT implementation | หลัง architect-agent approve แผน |
| `frontend-specialist-agent` | Next.js App Router + TypeScript | หลัง architect-agent approve แผน |
| `db-migration-agent` | Supabase migrations + RLS | ทุก schema change |
| `devops-orchestrator-agent` | CI/CD + Azure + deployment | Pipeline, infra, env vars |
| `quality-auditor-agent` | Pre-merge review + AOT + coverage | ก่อนสร้าง PR เสมอ |
| `feature-agent` | Full-stack coordinator | งานที่ต้องทำทั้ง backend + frontend พร้อมกัน |
| `pr-agent` | PR creation | หลัง quality-auditor-agent PASS เท่านั้น |

---

## Communication Protocol

```
User Request
    │
    ▼
orchestrator-agent  ←── reads .claude/memory/active-context.md
    │
    ├── Simple/clear request ──► route directly to specialist
    │
    └── New feature ──► architect-agent (ALWAYS FIRST)
                            │
                            ├──► backend-engineer-agent (C# implementation)
                            ├──► frontend-specialist-agent (Next.js UI)
                            ├──► db-migration-agent (schema change)
                            │
                            └──► quality-auditor-agent ──► pr-agent
```

**Hard rule:** `architect-agent` ต้อง approve plan ก่อน implement ทุกครั้ง — ไม่มีข้อยกเว้น

---

## Proven Patterns — Use These

### 1. Interface-First Decomposition
เมื่อ backend + frontend ทำงานพร้อมกัน ให้ define API contract ก่อน implement:
```csharp
// Core: define port interface first
public interface ICreateTenantUseCase {
    Task<Result<TenantId>> ExecuteAsync(CreateTenantCommand command);
}
```
จากนั้น frontend mock ไว้ก่อน แล้วค่อย implement ทั้งสองฝั่งพร้อมกัน

### 2. AOT-Safe JSON Pattern (บังคับสำหรับทุก endpoint ใหม่)
```csharp
// ลงทะเบียนทุก type ใน RylaJsonContext เสมอ
[JsonSerializable(typeof(CreateTenantRequest))]
[JsonSerializable(typeof(CreateTenantResponse))]
public partial class RylaJsonContext : JsonSerializerContext { }
```
ถ้าลืม → `make aot-check` จะ fail ด้วย IL2026

### 3. Result Pattern สำหรับ Error Propagation
```csharp
// ไม่ throw exception สำหรับ business errors
public async Task<Result<OrderId>> ProcessWebhookAsync(WebhookPayload payload)
{
    if (!_hmacVerifier.Verify(payload))
        return Result.Failure<OrderId>("invalid_signature");
    // ...
}
```

### 4. Webhook Pattern (TikTok Shop / Shopee)
ทุก webhook endpoint ต้อง:
1. HMAC verify **ก่อน** ทุกอย่าง
2. Return `200 OK` ใน < 3 วินาที
3. Queue processing แยกออกไปถ้างานหนัก
4. `[AllowAnonymous]` พร้อม comment อธิบาย

### 5. Supabase RLS Pattern
ทุก table ใหม่ต้อง enable RLS ทันทีในไฟล์ migration:
```sql
ALTER TABLE xxx ENABLE ROW LEVEL SECURITY;
CREATE POLICY "tenant_isolation" ON xxx
  USING (tenant_id = (SELECT tenant_id FROM profiles WHERE user_id = auth.uid()));
```

---

## Pitfalls — ห้ามทำโดยเด็ดขาด

### ❌ ห้ามใช้ `.WithOpenApi()` บน minimal API
```csharp
// WRONG — IL2026 AOT warning
app.MapPost("/webhooks/tiktok", handler).WithOpenApi();

// CORRECT
app.MapPost("/webhooks/tiktok", handler)
   .WithSummary("TikTok Shop webhook receiver")
   .WithTags("Webhooks")
   .Produces(200);
```

### ❌ ห้ามใช้ Moq — ใช้ NSubstitute เท่านั้น
```csharp
// WRONG — Moq ใช้ IL emit → AOT incompatible
var mock = new Mock<ILineNotificationAdapter>();

// CORRECT
var mock = Substitute.For<ILineNotificationAdapter>();
```

### ❌ ห้ามใช้ reflection-based DI scanning
```csharp
// WRONG
services.Scan(scan => scan.FromAssemblyOf<Anchor>()...);

// CORRECT
services.AddScoped<ICreateTenantUseCase, CreateTenantUseCase>();
services.AddScoped<ITikTokWebhookAdapter, TikTokWebhookAdapter>();
```

### ❌ ห้าม `mcp__supabase__execute_sql` โดยตรง
Schema changes ต้องผ่าน `db-migration-agent` เท่านั้น → migration files เท่านั้น

### ❌ ห้ามแก้ `auth.ts` และ `session.ts` พร้อมกันในหลาย branch
Circular imports ทำให้ test failures — ต้องแก้ทีละไฟล์

### ❌ ห้าม `git push --force` ทุกกรณี
Protected ใน settings.json deny list — ถ้าต้องการ rebase ให้ขอ dev อนุมัติก่อน

---

## Test Naming Convention (บังคับ)

```
{MethodUnderTest}__{Scenario}__{ExpectedOutcome}

// Examples:
ProcessWebhook__WhenHmacInvalid__ShouldReturnUnauthorized
ProcessWebhook__WhenPayloadValid__ShouldPublishLineNotification
CreateTenant__WhenEmailDuplicate__ShouldReturnConflict
```

---

## Task Claiming (Agent Teams)

เมื่อใช้ parallel agent teams, agents claim tasks ผ่าน `.claude/tasks/`:
```
.claude/tasks/
├── RYLA-123-backend.claimed      # backend-engineer-agent claimed
├── RYLA-123-frontend.claimed     # frontend-specialist-agent claimed
└── RYLA-123-migration.pending    # รอ db-migration-agent
```
Lock file format: `{ticket}-{scope}.{claimed|pending}`

---

## Ryla Domain Context

| Platform | เชื่อมต่อแบบ | Protocol |
|---|---|---|
| TikTok Shop | Inbound webhook | HMAC-SHA256 |
| Shopee | Inbound webhook | HMAC-SHA256 |
| LINE OA | Outbound push/reply | Bearer token |
| Google Sheets | Outbound append/update | OAuth2 service account |

**Core flow:** Platform event → Ryla webhook → process → LINE notification / Sheets update

---

## Changelog Fragment Schema

ทุก PR ต้องมี fragment ใน `changelog/fragments/{PR_NUMBER}-{slug}.yml`:

```yaml
pr: 42
type: feat                    # feat|fix|perf|refactor|security|docs|chore
scope: "api"                  # api|core|infra|frontend|supabase|ci
title: "Add TikTok webhook receiver"   # < 80 chars, user-facing
description: |                # optional — อธิบาย user impact
  Ryla can now receive and verify TikTok Shop order events.
breaking: false
migration: false              # true ถ้า PR มี Supabase migration
```

ยกเว้น PR ที่มี label `skip-changelog` (CI config, deps update, release commits)

---

## Per-Session Checklist

เมื่อเริ่ม session ใหม่ให้ `orchestrator-agent` อ่าน:
- [ ] `.claude/memory/active-context.md` — sprint state ปัจจุบัน
- [ ] `.claude/memory/feature-state.md` — feature ที่กำลัง implement

เมื่อ session จบ:
- [ ] อัพเดท `active-context.md` ถ้ามี milestone เสร็จ
- [ ] อัพเดท `feature-state.md` ถ้า feature phase เปลี่ยน
- [ ] เพิ่ม pattern ใหม่ใน AGENTS.md นี้ถ้าเจอ
- [ ] เพิ่ม AOT pitfall ใหม่ใน `.claude/memory/aot-unsafe-patterns.md` ถ้าเจอ
