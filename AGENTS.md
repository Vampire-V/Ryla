# AGENTS.md — Ryla AI Team Handbook

> **IMPORTANT:** ไฟล์นี้เขียนและดูแลโดย developer เท่านั้น — agents ห้ามแก้ไขโดยตรง
> อัพเดทหลัง session ที่มี learning ใหม่เท่านั้น

---

## Team Roster

| Agent | บทบาท | เรียกใช้เมื่อ |
|---|---|---|
| `orchestrator-agent` | Entry point — routing meta-agent | Request ไม่ชัดเจน, งานข้ามหลาย layer |
| `architect-agent` | System design + AOT compliance | Path A และ Path C ที่เปลี่ยน public interface/boundary |
| `backend-engineer-agent` | .NET 10 + AOT implementation | งาน backend ที่ไม่ต้อง coordinate กับ frontend |
| `frontend-specialist-agent` | Next.js App Router + TypeScript | งาน frontend ที่ไม่ต้อง coordinate กับ backend |
| `feature-agent` | Full-stack coordinator | Path A ที่ backend + frontend ต้อง coordinate (เช่น API contract ใหม่) |
| `db-migration-agent` | Supabase migrations + RLS | Path D — ทุก schema change ไม่มีข้อยกเว้น |
| `devops-orchestrator-agent` | CI/CD + Azure + deployment | Path E — pipeline, infra, env vars |
| `quality-auditor-agent` | Pre-merge review + AOT + coverage | Path A–D, H(post-merge) ก่อน PR (optional สำหรับ Path E, F) |
| `pr-agent` | PR creation | หลัง Human Approval Gate ทุกครั้ง |

---

## Communication Protocol

orchestrator-agent จัดประเภท request และเลือก path ที่เหมาะสม:

```
User Request
    │
    ▼
orchestrator-agent  ←── reads .claude/memory/active-context.md
    │
    ├── [Q] Question / exploration (ไม่ต้องเปลี่ยนโค้ด)
    │       → ตอบโดยตรง หรือ dispatch Explore agent
    │         ไม่ต้อง branch, ไม่ต้อง PR, ไม่ต้อง gate
    │
    ├── [A] New feature (non-trivial)
    │       → architect-agent → implement (ดูเกณฑ์ feature-agent ด้านล่าง)
    │         → [Full Approval Gate] → quality-auditor-agent → pr-agent → develop
    │
    ├── [B] Bug fix
    │       → ลอง fix เองก่อน (อ่าน error, trace code, ลอง reproduce)
    │         → ถ้าหลัง 2 attempts ยังไม่พบ root cause → systematic-debugging skill
    │         → backend/frontend-agent → unit test
    │         → [Full Approval Gate] → quality-auditor-agent → pr-agent → develop
    │         ⚠️ ถ้า bug fix ต้องการ schema change → ผ่าน db-migration-agent ด้วย
    │
    ├── [C] Refactor
    │       → (เปลี่ยน public interface หรือ layer boundary) architect-agent → implement → all tests
    │         → [Full Approval Gate] → quality-auditor-agent → pr-agent → develop
    │         (internal-only: rename, extract method, move file) implement โดยตรง → tests
    │         → [Full Approval Gate] → pr-agent → develop
    │
    ├── [D] Schema change / migration
    │       → db-migration-agent → [Full Approval Gate] → quality-auditor-agent → pr-agent → develop
    │
    ├── [E] CI / DevOps / infra
    │       → devops-orchestrator-agent → [Lite Approval Gate] → pr-agent → develop
    │
    ├── [F] Chore / docs / dependency update
    │       → implement โดยตรง → [Lite Approval Gate] → pr-agent → develop
    │
    └── [H] Hotfix (production incident)
            ⚠️ ใช้เมื่อ user พูดว่า "production", "incident", "down", "urgent fix" เท่านั้น
            → implement โดยตรง → unit test → [Full Approval Gate] → pr-agent → main (!)
            → หลัง merge: orchestrator สร้าง PR back-merge main → develop ทันที
```

**PR target:** ทุก path → PR ไปที่ **`develop`** ยกเว้น Path H → `main`

---

**Full Approval Gate** (Path A–D, H — งานที่กระทบ runtime behavior):
```
1. git log develop..HEAD --oneline    (commits ทั้งหมด)
2. git diff develop --stat            (scope ของงาน)
3. ถ้ามี E2E scripts ที่เกี่ยวข้อง (ls tests/e2e/) →
   ถ้า app ไม่ได้รัน → ถาม user: "ต้องรัน E2E — เริ่ม app ให้เลยไหม?"
   ถ้า user ตอบ yes → dotnet run ... & แล้วรอ health check pass
   → make test-e2e
4. ถาม user: "พร้อม create PR ไหม?" + แสดง summary
5. รอ user ตอบก่อนเดินหน้า
    ↓ (user approve)
quality-auditor-agent (Path H ข้ามได้เพราะเร่งด่วน แต่ต้อง review หลัง merge ภายใน 24h)
    ↓
pr-agent
```

**Lite Approval Gate** (Path E, F — ไม่กระทบ runtime behavior):
```
1. git log develop..HEAD --oneline    (commits ทั้งหมด)
2. git diff develop --stat            (scope ของงาน)
3. ถาม user: "พร้อม create PR ไหม?"
4. รอ user ตอบก่อนเดินหน้า
    ↓ (user approve)
pr-agent (ข้าม quality-auditor-agent)
```

---

**Path selection rules:**
- คำถาม / อ่าน code / วิเคราะห์ → Path Q (ไม่ต้อง PR)
- New feature + ใช้เวลา > ครึ่งวัน หรือ unclear feasibility → Path A
- Bug fix ทุกขนาด → Path B (bug เล็กก็คือ Path B ไม่แยก path ให้สับสน)
- Bug ที่ต้องการ schema change → Path B + Path D ทำต่อเนื่องกัน
- Refactor ที่เปลี่ยน public interface / port / adapter boundary → Path C ผ่าน architect-agent
- Refactor ที่เป็น internal-only (rename, move, extract) → Path C ไม่ต้องผ่าน architect
- ทุก schema change ไม่ว่าเล็กแค่ไหน → ผ่าน db-migration-agent เสมอ
- **ถ้าสงสัยว่าเป็น path ไหน → ถาม user ก่อน route ห้ามเดาเอง**

**feature-agent vs specialists — เกณฑ์เลือก:**
- feature-agent: งานที่ backend + frontend ต้อง coordinate (เช่น API contract ใหม่ที่ frontend ต้องเรียก)
- backend-engineer-agent + frontend-specialist-agent แยก: งานที่ 2 ฝั่งทำแยกได้ไม่ขึ้นกัน (เช่น backend webhook + frontend dashboard page ที่ไม่เกี่ยวกัน)
- ถ้ามีแค่ฝั่งเดียว → specialist ตัวเดียว ไม่ต้องใช้ feature-agent

**Hard rules:**
- `architect-agent` บังคับสำหรับ Path A และ Path C (เปลี่ยน public interface) เท่านั้น
- `feature-agent` ใช้สำหรับ Path A ที่ backend + frontend ต้อง coordinate เท่านั้น
- Human Approval Gate (Full หรือ Lite) บังคับก่อน PR ทุก path — ไม่มีข้อยกเว้น
- ห้าม call `pr-agent` โดยไม่ผ่าน Human Approval Gate

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

## Task Claiming (Agent Teams) — Experimental

> ⚠️ ยังไม่ได้ใช้จริงใน production workflow — ใช้เมื่อ dispatch parallel subagents เท่านั้น

เมื่อใช้ `subagent-driven-development` skill กับ parallel teams, agents claim tasks ผ่าน `.claude/tasks/`:
```
.claude/tasks/
├── RYLA-123-backend.claimed      # backend-engineer-agent กำลังทำอยู่
├── RYLA-123-frontend.claimed     # frontend-specialist-agent กำลังทำอยู่
└── RYLA-123-migration.pending    # รอ db-migration-agent รับงาน
```
Lock file format: `{ticket}-{scope}.{claimed|pending}`

Lifecycle: orchestrator สร้าง `.pending` → agent สร้าง `.claimed` → orchestrator ลบเมื่อ task เสร็จ
ถ้า agent fail กลางคัน: orchestrator ลบ `.claimed` แล้ว dispatch ใหม่

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

## Pre-PR Checklist (Human Approval Gate)

ขั้นตอนนี้ทำโดย **orchestrator ร่วมกับ user** ไม่ใช่ subagent ทำเองโดยอัตโนมัติ

**ทุก path (Full + Lite):**
- [ ] แสดง `git log develop..HEAD --oneline` ให้ user เห็น commits ทั้งหมด
- [ ] แสดง `git diff develop --stat` ให้ user เห็น scope ของงาน
- [ ] **User ตอบ "พร้อม create PR" อย่างชัดเจน** ← gate ตัวสุดท้าย

**เพิ่มเติมสำหรับ Full Gate (Path A–D, H):**
- [ ] `make quality-gate` ผ่าน (lint + unit tests + AOT + changelog)
- [ ] `make test-e2e` ผ่าน — ถ้า app ไม่ได้รัน ให้ถาม user ก่อนเริ่ม
- [ ] `tests/e2e/{feature}.py` ถูก commit เข้า branch แล้ว (ถ้า feature มี API endpoint)

---

## Per-Session Checklist

**orchestrator-agent ทำเมื่อเริ่ม session:**
- [ ] อ่าน `.claude/memory/active-context.md` — sprint state ปัจจุบัน
- [ ] อ่าน `.claude/memory/feature-state.md` — feature ที่กำลัง implement

**orchestrator-agent ทำเมื่อ feature milestone เสร็จ:**
- [ ] อัพเดท `active-context.md` — milestone ใหม่, active branch, next up
- [ ] อัพเดท `feature-state.md` — ย้าย feature จาก In Progress → Completed

**developer ทำเอง (ไม่ใช่ agent):**
- [ ] เพิ่ม pattern ใหม่ใน `AGENTS.md` ## Proven Patterns ถ้าเจอ pattern ที่ reusable
- [ ] เพิ่ม AOT pitfall ใหม่ใน `AGENTS.md` ## Pitfalls ถ้าเจอ violation ใหม่
