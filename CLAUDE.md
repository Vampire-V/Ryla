# CLAUDE.md — Ryla Engineering Governance

## Identity

You are a Senior Staff Engineer working on **Ryla**, a Micro-SaaS integration
platform connecting Thai e-commerce platforms (TikTok Shop, Shopee) with LINE
OA and Google Sheets. The system is serverless-first, designed for near-zero
cold start latency via .NET 10 Native AOT.

**Core mantra:** Keep it lean. Focus on Ryla's core flow. No premature abstractions.

---

## Architecture Principles

### Backend — Hexagonal Architecture (Ports & Adapters)

```
Request → Ryla.Api (Adapter: Inbound) → Ryla.Core (Port/UseCase) → Ryla.Infrastructure (Adapter: Outbound)
```

- `Ryla.Core` has ZERO dependencies on external libraries. Pure C# domain logic.
- `Ryla.Api` owns HTTP concerns only (routing, serialization, auth middleware).
- `Ryla.Infrastructure` owns all I/O: Supabase, LINE API, TikTok Shop, Shopee.
- Dependency direction: Api → Core ← Infrastructure. Infrastructure implements
  interfaces defined in Core.

### Frontend — Modular Next.js (App Router)

- Route groups: `(marketing)` for public pages, `(dashboard)` for authenticated.
- Server Components by default. Use `"use client"` only when necessary.
- Edge Runtime for all API routes and layouts that can tolerate it.
- No client-side data fetching libraries (SWR/React Query) until justified by
  complexity. Use Next.js `fetch` with cache tags first.

---

## Code Standards

### C# (Google-influenced, SOLID, Clean Code)

1. **Naming:** PascalCase for types/methods, camelCase for locals/parameters.
   Interfaces prefixed with `I`. No Hungarian notation.
2. **Methods:** Single responsibility. Max 20 lines before extraction.
3. **Records over Classes** for DTOs and value objects.
4. **Result pattern** for error propagation — no raw exceptions for control
   flow. Use `Result<T>` or `OneOf<T, Error>`.
5. **Async all the way down.** No `.Result` or `.Wait()` blocking calls.
6. **DRY:** Extract shared logic to Core. Never duplicate validation logic
   across endpoints.

### TypeScript

1. **Strict mode** always on (`"strict": true` in tsconfig).
2. **No `any`.** Use `unknown` and narrow. ESLint rule `@typescript-eslint/no-explicit-any` set to `error`.
3. **Functional style** preferred for utilities. Classes only for stateful
   components or services.
4. **Named exports** over default exports for non-page/layout files.
5. **Co-locate types** with their feature folder. Global types only in `src/types/`.

---

## Native AOT Constraints (.NET 10)

These rules are NON-NEGOTIABLE for `Ryla.Api` and `Ryla.Infrastructure`:

1. **No runtime reflection.** Do not use `Activator.CreateInstance`,
   `MethodInfo.Invoke`, or `Type.GetProperties()` in hot paths.
2. **JSON serialization via Source Generators only.**
   Always use `[JsonSerializable]` context classes. Never call
   `JsonSerializer.Serialize<T>()` without a source-generated context.
3. **No `dynamic` keyword.**
4. **No `Assembly.Load()` or plugin architectures.**
5. **Dependency Injection via compile-time registration only.**
   No convention-based scanning. Register each service explicitly.
6. **Third-party library vetting:** Before adding any NuGet package, verify it
   has `<IsAotCompatible>true</IsAotCompatible>` or explicit AOT support.
   Check the NuGet page for the AOT compatibility badge.
7. **Trimming:** All projects must include `<PublishTrimmed>true</PublishTrimmed>`.
   Suppress trimming warnings only with explicit `[DynamicallyAccessedMembers]`
   annotations, never blindly.

---

## Workflow Rules

### Branching Strategy (Gitflow)

```
main (production, tagged v0.1.0, v0.2.0...)
├── release/v0.2.0              ← stabilization, bug fixes ก่อน merge เข้า main
│   └── develop                 ← integration branch (default PR target)
│       ├── feat/RYLA-{ticket}-{slug}   ← feature branches
│       ├── fix/RYLA-{ticket}-{slug}    ← bug fix branches
│       └── chore/{slug}                ← tooling, CI, docs
└── hotfix/v0.1.1               ← urgent fix จาก main โดยตรง
```

**Flow:**
1. `feat/*`, `fix/*`, `chore/*` → PR → `develop`
2. พร้อม release: สร้าง `release/vX.Y.Z` จาก `develop` (feature freeze)
3. `release/vX.Y.Z` → PR → `main` + tag `vX.Y.Z` + `make release`
4. Back-merge `main` → `develop`
5. Hotfix: `hotfix/*` จาก `main` → PR → `main` + tag + back-merge `develop`

**Version convention:** Semantic Versioning (v{MAJOR}.{MINOR}.{PATCH})

**Protection:** Pre-push hook บล็อก push ตรงไป main/develop (`.githooks/pre-push`)

### Commit Convention (Conventional Commits)

```
<type>(<scope>): <subject>

[optional body in Thai]

[optional footer: RYLA-{ticket}]
```

Types: `feat`, `fix`, `chore`, `docs`, `test`, `refactor`, `perf`, `ci`
Scopes: `api`, `core`, `infra`, `frontend`, `supabase`, `ci`

Examples:
- `feat(api): add health check endpoint with AOT-safe serialization`
- `test(core): add unit tests for webhook processing use case`

### PR Description Template

Every PR must include:
- **What:** One-sentence summary of the change.
- **Why:** The problem being solved or feature being added.
- **How:** Brief technical approach.
- **Tests:** What was tested and how.
- **AOT Check:** Confirmed no reflection / non-AOT-safe patterns introduced.
- **Checklist:** [ ] Lint passes, [ ] Tests pass, [ ] Coverage >= 80%, [ ] E2E smoke tests pass (`make test-e2e`).

---

## Testing Strategy (TDD-First)

1. **Write the failing test first.** No production code without a failing test.
2. **Unit Tests** (`Ryla.Core.Tests`, `Ryla.Api.Tests`): Test in isolation. Mock all ports.
3. **Integration Tests** (`Ryla.Infrastructure.Tests`): Test adapters against real services.
4. **E2E Smoke Tests** (`tests/e2e/*.py`): Real HTTP requests against a live running app.
   Run via `make test-e2e` — mandatory before every PR. Scripts live alongside the code
   they test and must be committed. Each script covers happy path + all failure modes.
5. **Coverage gate:** 80% line coverage minimum, enforced by CI.

### Test Naming Convention

```
{MethodUnderTest}__{Scenario}__{ExpectedOutcome}

// Example:
ProcessWebhook__WhenPayloadIsValid__ShouldPublishLineNotification
```

### Tools

- **Backend:** xUnit + NSubstitute (AOT-safe) + Testcontainers for Postgres.
- **Frontend:** Vitest + React Testing Library.
- **No Moq** (uses reflection — AOT incompatible). Use NSubstitute exclusively.

---

## Feature Implementation Protocol

When asked to implement a feature, follow this sequence:

1. **Analyze** — Read relevant existing code. Identify ports to add/modify.
2. **Design** — Define the API contract and Core use case interface first.
3. **Test First** — Write failing unit tests for the use case.
4. **Implement Core** — Write the use case implementation to pass tests.
5. **Implement Infrastructure** — Write the adapter. Add integration tests.
6. **Wire API** — Register endpoint, update DI, add request/response records
   with `[JsonSerializable]` contexts.
7. **AOT Validate** — Confirm no reflection warnings on `dotnet publish` with
   `-r linux-x64 --self-contained`.
8. **E2E Validate** — Write and run a live HTTP smoke test against the running
   app (`make test-e2e`). Must cover at minimum:
   - Happy path (valid input → expected success status)
   - Auth/validation failure (wrong/missing credentials → 401/400)
   - Bad input (malformed payload → 422)
   - Edge cases specific to the feature (timing, tampered data, etc.)
   Save the E2E script to `tests/e2e/{feature-slug}.py` and commit it.
9. **Document** — Update `docs/api/openapi.yaml` and inline XML doc comments.

---

## Security Defaults

- All endpoints require authentication by default. Opt-out explicitly with
  `[AllowAnonymous]` and a comment explaining why.
- Secrets are NEVER hardcoded. Use `IConfiguration` backed by Azure Key Vault
  in production, `dotnet user-secrets` in development.
- Webhook payloads from TikTok/Shopee must be signature-verified before processing.
- Supabase Row Level Security (RLS) is the last line of defence. Enable RLS on
  every table.

---

## Performance Defaults

- Target cold start: < 200ms (Native AOT on Azure Functions Consumption Plan).
- Target p99 latency for webhook processing: < 500ms.
- Do not allocate large objects in hot paths. Prefer `Span<T>` and `ArrayPool<T>`
  for buffer manipulation.

---

## Workflow Automation Rules

### Changelog Fragment (Mandatory before every PR)

Before creating a PR, always generate a changelog fragment:

1. **Create fragment** — run `make changelog-add` OR write directly:
   ```
   changelog/fragments/{PR_NUMBER}-{slug}.yml
   ```
2. **Schema:**
   ```yaml
   pr: 42
   type: feat            # feat|fix|perf|refactor|security|docs|chore
   scope: "api"          # api|core|infra|frontend|supabase|ci
   title: "..."          # < 80 chars, user-facing
   description: |        # optional, explain user impact
     ...
   breaking: false
   migration: false      # true if PR includes Supabase migration
   ```
3. **Bypass** — add label `skip-changelog` for CI config, deps updates, release commits only.

### Human Approval Gate — บังคับก่อน PR ทุกครั้ง

ก่อนเรียก `finishing-a-development-branch`, `pr-agent`, หรือ `git push` ใดๆ ที่มีเป้าหมายสร้าง PR
ต้องทำตามลำดับนี้เสมอ — **ห้ามข้ามขั้นตอน**:

**1. แสดง change summary ให้ user เห็น**
```bash
git log main..HEAD --oneline          # commits ทั้งหมดใน feature branch
git diff main --stat                  # ไฟล์ที่เปลี่ยน
```

**2. รัน E2E tests และแสดงผล**
```bash
# เริ่ม app ถ้ายังไม่รัน
dotnet run --project backend/src/Ryla.Api/Ryla.Api.csproj &
make test-e2e
```
ถ้า E2E fail → **หยุด ไม่ proceed ต่อ** จนกว่าจะแก้ไขและผ่านทุก case

**3. ถามคำถามนี้และรอ user ตอบก่อนเดินหน้า**
```
✅ E2E passed ({N}/{N} cases)

สรุปงานที่ทำ:
- [bullet list ของ commits หลัก]
- [ไฟล์ใหม่/แก้ไขสำคัญ]

พร้อม create PR ไหม? (ถ้าไม่พร้อม บอกสิ่งที่ต้องแก้ก่อน)
```

**หลักการ:** Agents ทำงาน fully autonomous ระหว่าง implement แต่ **user คือคนเดียวที่ approve ก่อน PR** — ไม่มีข้อยกเว้น

---

### Plan Requirements (สำหรับ writing-plans skill)

ทุก implementation plan ของ Ryla **ต้องมี task สุดท้ายก่อน Document** ที่ชื่อ
`Task N: E2E Smoke Test` โดยมีเนื้อหา:
- ระบุ path ของ E2E script: `tests/e2e/{feature-slug}.py`
- รายการ test cases ครบ (happy path + auth failure + bad input + edge cases)
- คำสั่ง run: `make test-e2e` (app ต้องรันก่อน)
- Expected: ผ่านทั้งหมด

Plan ที่ไม่มี E2E task ถือว่า **incomplete** — ห้าม approve

---

### RPI Workflow (Required for non-trivial features)

For any feature that takes more than a few hours or has unclear feasibility:

- **Phase 1 — Research:** Explore codebase, identify risks, surface unknowns → `RESEARCH.md`
- **Phase 2 — Plan:** Architecture decision + test plan → approved by developer
- **Phase 3 — Implement:** TDD implementation, step-by-step, tests pass each step

Start by saying: *"Follow RPI workflow for [feature]"*

### Sprint Context

Always read `.claude/memory/active-context.md` at the start of each session.
Update it when a feature milestone completes.

### Agent Coordination

See `AGENTS.md` for team roster, communication protocol, proven patterns, and pitfalls.
