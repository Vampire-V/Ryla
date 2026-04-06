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

### Branching Strategy

```
main          ← production-ready, protected
└── develop   ← integration branch
    └── feat/RYLA-{ticket}-{slug}   ← feature branches
    └── fix/RYLA-{ticket}-{slug}    ← bug fix branches
    └── chore/{slug}                ← tooling, CI, docs
```

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
- **Checklist:** [ ] Lint passes, [ ] Tests pass, [ ] Coverage >= 80%.

---

## Testing Strategy (TDD-First)

1. **Write the failing test first.** No production code without a failing test.
2. **Unit Tests** (`Ryla.Core.Tests`, `Ryla.Api.Tests`): Test in isolation. Mock all ports.
3. **Integration Tests** (`Ryla.Infrastructure.Tests`): Test adapters against real services.
4. **Coverage gate:** 80% line coverage minimum, enforced by CI.

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
8. **Document** — Update `docs/api/openapi.yaml` and inline XML doc comments.

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
