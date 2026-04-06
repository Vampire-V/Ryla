# Ryla — AI Team Workflow & Organization

> Micro-SaaS integration platform: TikTok Shop / Shopee → LINE OA / Google Sheets

---

## Organization Structure

```
                        USER
                          │
                          ▼
              ┌─────────────────────┐
              │   orchestrator-agent │  ← Entry point / Router
              │   "The Dispatcher"  │    อ่าน active-context.md ก่อนทุกครั้ง
              └──────────┬──────────┘
                         │
          ┌──────────────┼──────────────┐
          │              │              │
          ▼              ▼              ▼
   ┌─────────────┐ ┌──────────┐ ┌────────────────┐
   │  architect  │ │  devops- │ │quality-auditor │
   │   -agent    │ │orchestr. │ │    -agent      │
   │  "The CTO"  │ │"DevOps"  │ │  "QA Lead"     │
   └──────┬──────┘ └──────────┘ └───────┬────────┘
          │                              │
    ┌─────┴──────┐                  ┌────▼────┐
    │            │                  │pr-agent │
    ▼            ▼                  │"Scribe" │
┌───────┐  ┌─────────┐             └─────────┘
│backend│  │frontend │
│-eng.  │  │-spec.   │
│"Dev"  │  │"Dev"    │
└───┬───┘  └────┬────┘
    │            │
    └─────┬──────┘
          ▼
  ┌──────────────┐
  │db-migration  │
  │   -agent     │
  │    "DBA"     │
  └──────────────┘
```

### Team Roster

| Agent | บทบาท | เรียกใช้เมื่อ |
|---|---|---|
| `orchestrator-agent` | Router / Dispatcher | Request ไม่ชัดเจน, งานข้ามหลาย layer |
| `architect-agent` | System design + AOT compliance | **ก่อน implement ทุก feature** (mandatory) |
| `backend-engineer-agent` | .NET 10 + AOT implementation | หลัง architect approve แผน |
| `frontend-specialist-agent` | Next.js App Router + TypeScript | หลัง architect approve แผน |
| `db-migration-agent` | Supabase migrations + RLS | ทุก schema change |
| `devops-orchestrator-agent` | CI/CD + Azure + deployment | Pipeline, infra, env vars |
| `quality-auditor-agent` | Pre-merge review + coverage | ก่อนสร้าง PR เสมอ |
| `feature-agent` | Full-stack coordinator | งานที่ต้องทำทั้ง backend + frontend พร้อมกัน |
| `pr-agent` | PR creation | หลัง quality-auditor PASS เท่านั้น |

---

## Flow การทำงาน

### Feature ใหม่ (Full Flow)

```
User: "implement TikTok webhook receiver"
        │
        ▼
[UserPromptSubmit hook]  ← รันอัตโนมัติก่อน Claude รับ prompt
  - inject sprint context (active-context.md)
  - detect "implement + feature" → suggest RPI workflow
        │
        ▼
[orchestrator-agent]
  - อ่าน AGENTS.md + memory files
  - route ไปยัง specialist ที่เหมาะสม
        │
        ▼
[architect-agent]  ← MANDATORY FIRST
  - ออกแบบ Hexagonal layers, API contract, DB schema
  - AOT compatibility check
  - Output: Architecture Decision document
        │
        ▼ (developer approves plan)
        │
   ┌────┴────┐
   ▼         ▼  (parallel — agent teams)
[backend]  [db-migration-agent]
[engineer]
  - TDD: test first, then implement
  - Implement Core + Api + AOT-safe JSON
        │
        ▼
[quality-auditor-agent]
  - Lint + tests + AOT check
  - PASS / FAIL verdict
        │
        ▼ (PASS only)
[UserPromptSubmit hook]  ← detect PR intent → remind changelog
        │
        ▼
make changelog-add  →  สร้าง changelog/fragments/{PR}-{slug}.yml
        │
        ▼
[pr-agent]
  - สร้าง branch + PR พร้อม description
        │
        ▼
[CI: quality-gate.yml]
  - changelog-fragment check
  - migration flag check
  - lint + test + AOT build
```

---

### ทุก Prompt — Auto Context Injection

```
User พิมพ์อะไรก็ตาม
        │
        ▼
[UserPromptSubmit hook] รันทันที:
  1. อ่าน .claude/memory/active-context.md
  2. inject เป็น context → Claude รู้ sprint state เสมอ
  3. ตรวจ intent:
       PR intent?   → remind changelog fragment
       Feature?     → suggest RPI workflow
       NuGet?       → remind AOT compatibility check
        │
        ▼
Claude รับ context ครบก่อนตอบ
```

---

### RPI Workflow — Feature ที่ซับซ้อน

```
Phase 1 — Research
  "research feasibility of [feature]"
  → Claude explore codebase, ระบุ risks, ตอบ unknowns
  → Output: RESEARCH.md

Phase 2 — Plan
  "create architecture plan for [feature]"
  → architect-agent ออก Architecture Decision
  → Developer approve ก่อนเขียนโค้ดใบ

Phase 3 — Implement
  "implement [feature] following the plan"
  → TDD: tests first → implementation → AOT check
```

---

### Changelog Fragment — 3-Layer Enforcement

| Layer | กลไก | เวลา |
|---|---|---|
| Layer 1 | `CLAUDE.md` rule — Claude รู้และทำตาม | ทุก session |
| Layer 2 | `UserPromptSubmit` hook — detect PR intent | ก่อน prompt |
| Layer 3 | `quality-gate.yml` CI — block merge ถ้าไม่มี | ก่อน merge |

**Fragment schema** (`changelog/fragments/{PR}-{slug}.yml`):
```yaml
pr: 42
type: feat          # feat|fix|perf|refactor|security|docs|chore
scope: "api"        # api|core|infra|frontend|supabase|ci
title: "..."        # < 80 chars, user-facing
description: |
  อธิบาย user-facing impact
breaking: false
migration: false    # true ถ้ามี Supabase migration ใน PR
```

---

## Living Memory — "Company Brain"

```
.claude/memory/
├── active-context.md         ← sprint state (inject ทุก session อัตโนมัติ)
├── architecture-decisions.md ← ADR log
├── aot-unsafe-patterns.md    ← IL warnings เคยเจอ + วิธี fix
└── feature-state.md          ← backlog, in-progress, completed

AGENTS.md                     ← company handbook (ทุก agent อ่าน)
```

---

## Parallel Agent Teams

เมื่อ `CLAUDE_CODE_EXPERIMENTAL_AGENT_TEAMS=1` (enabled ใน settings.json):

```
architect-agent วาง plan แล้ว →

.claude/tasks/
├── RYLA-123-backend.claimed    ← backend-engineer-agent claim แล้ว
├── RYLA-123-frontend.claimed   ← frontend-specialist-agent claim แล้ว
└── RYLA-123-migration.pending  ← รอ db-migration-agent

agents 3 ตัวทำงานพร้อมกัน แต่ละตัวมี context 1M tokens แยกกัน
สื่อสารผ่าน peer-to-peer mailbox system
```

---

## Make Commands

```bash
# Development
make dev              # เริ่ม local dev stack
make test             # รัน tests ทั้งหมด
make build-aot        # build Native AOT binary

# Quality
make quality-gate     # lint + test + AOT + changelog (ก่อน PR)
make aot-check        # ตรวจ AOT trim warnings เร็ว
make security-check   # NuGet + npm vulnerability scan

# Workflow Automation
make changelog-add    # สร้าง changelog fragment สำหรับ PR
make changelog-check  # ตรวจว่ามี fragment แล้วหรือยัง
make context-update   # แก้ไข active-context.md (sprint state)
make agent-status     # ดู task claims + pending fragments

# Database
make db-migrate name=add_flows_table  # สร้าง migration ใหม่
make types                             # generate TypeScript types
```

---

## Security & Permissions

```
Deny list (ห้าม Claude ทำโดยตรง):
  - supabase db push / db reset   → ผ่าน devops-orchestrator เท่านั้น
  - mcp__supabase__execute_sql    → ผ่าน migration files เท่านั้น
  - git push --force              → ห้ามทุกกรณี
  - rm -rf                        → ห้าม

Pre-commit hook (อัตโนมัติ):
  - dotnet format --verify-no-changes
  - block commit ถ้า C# formatting ผิด
```
