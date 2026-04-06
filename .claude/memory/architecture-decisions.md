# Architecture Decision Log — Ryla
<!-- เพิ่ม ADR ใหม่เมื่อมีการตัดสินใจ architecture ที่สำคัญ -->

---

## ADR-001: WebApplication.CreateSlimBuilder (not CreateBuilder)

- **Date:** 2026-04-07
- **Decision:** ใช้ `WebApplication.CreateSlimBuilder()` เท่านั้น
- **Reason:** `CreateBuilder` เปิด reflection-based MVC conventions ซึ่ง AOT-incompatible
- **Consequence:** ใช้ Minimal API endpoints เท่านั้น — ไม่มี MVC controllers

---

## ADR-002: NSubstitute over Moq

- **Date:** 2026-04-07
- **Decision:** ใช้ NSubstitute เท่านั้นสำหรับ test mocking
- **Reason:** Moq ใช้ IL emit ที่ runtime — AOT-incompatible ทันที
- **Consequence:** ห้ามใช้ Moq ในทุก test project; quality-auditor-agent ตรวจสอบ

---

## ADR-003: No `.WithOpenApi()` on endpoints

- **Date:** 2026-04-07
- **Decision:** ใช้ `.WithSummary()`, `.WithTags()`, `.Produces<T>()` แทน
- **Reason:** `.WithOpenApi()` มี `[RequiresDynamicCode]` attribute — AOT-incompatible
- **Consequence:** OpenAPI spec ต้องอัพเดท `docs/api/openapi.yaml` manually หลัง add endpoint

---

## ADR-004: JSON Serialization via Source Generators (RylaJsonContext)

- **Date:** 2026-04-07
- **Decision:** ทุก HTTP boundary type ต้องลงทะเบียนใน `RylaJsonContext` ด้วย `[JsonSerializable]`
- **Reason:** `JsonSerializer.Serialize<T>()` ที่ไม่มี context ใช้ reflection — IL2026 warning
- **Consequence:** ทุก endpoint request/response type ต้องเพิ่มใน `ServiceCollectionExtensions.cs` ก่อน AOT build

---

## ADR-005: Ryla.Core Zero NuGet Dependencies

- **Date:** 2026-04-07
- **Decision:** `Ryla.Core.csproj` ต้องไม่มี `<PackageReference>` entries เลย
- **Reason:** Core isolation รับประกัน testability โดยไม่มี infrastructure concerns
- **Consequence:** ทุก external library types (Npgsql, HttpClient, Newtonsoft) อยู่ใน `Ryla.Infrastructure` เท่านั้น

---

## ADR-006: Hexagonal Architecture Layer Boundaries

- **Date:** 2026-04-07
- **Decision:** Request → `Ryla.Api` → `Ryla.Core` → `Ryla.Infrastructure`; dependency direction: Api → Core ← Infrastructure
- **Reason:** Clean separation ให้ test use cases โดยไม่ต้องการ DB หรือ HTTP stack
- **Consequence:** Infrastructure implements interfaces ที่ defined ใน Core — Core ไม่รู้จัก Infrastructure

---

<!-- เพิ่ม ADR ใหม่ต่อจากนี้ -->
