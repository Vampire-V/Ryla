# AOT-Unsafe Patterns Registry — Ryla
<!-- อัพเดทเมื่อ encounter IL warning ใหม่และ fix แล้ว -->

---

## Known Patterns — Encountered and Fixed

### IL2026: Missing [JsonSerializable] Registration
- **Trigger:** เพิ่ม request/response type ใน endpoint โดยไม่ลงทะเบียน
- **Fix:** เพิ่ม `[JsonSerializable(typeof(XxxRequest))]` ใน `RylaJsonContext` ที่ `ServiceCollectionExtensions.cs`
- **Prevention:** `backend-engineer-agent` Step 4 มี checklist; AOT build ใน CI จะ block ด้วย `/warnaserror`

### IL2026: `.WithOpenApi()` on Minimal API endpoint
- **Trigger:** ใช้ `.WithOpenApi()` บน MapPost/MapGet/etc.
- **Fix:** ลบ `.WithOpenApi()` ออก ใช้ `.WithSummary()/.WithTags()/.Produces<T>()` แทน
- **Prevention:** `architect-agent`, `backend-engineer-agent`, `feature-agent` ทุก agent document rule นี้

### IL2072: Reflection-based DI Resolution
- **Trigger:** ใช้ `services.Scan()`, `services.AddFromAssembly()`, หรือ convention-based registration
- **Fix:** เปลี่ยนเป็น explicit `services.AddScoped<IFoo, Foo>()`
- **Prevention:** `DependencyInjection.cs` pattern enforces explicit registration; ห้าม scanning packages

### IL3050: Dynamic Code Generation
- **Trigger:** `Assembly.Load()`, `Activator.CreateInstance()`, `dynamic` keyword
- **Fix:** ลบออก ไม่มีทางใช้ร่วมกับ AOT ได้
- **Prevention:** Code review checklist ใน quality-auditor-agent

---

## Packages Vetted as AOT-Compatible ✅

| Package | Version Tested | Notes |
|---|---|---|
| NSubstitute | latest | Compile-time source gen via analyzer |
| Npgsql | latest | `IsAotCompatible=true` on NuGet |
| Microsoft.AspNetCore.OpenApi | .NET 10 built-in | Built-in |

---

## Packages That Are AOT-INCOMPATIBLE ❌ — Never Use

| Package | Reason |
|---|---|
| Moq | IL emit at runtime |
| Swashbuckle.AspNetCore | Reflection-based schema generation |
| AutoMapper | Reflection-based type mapping |
| Newtonsoft.Json | Reflection-based serialization (use STJ + source gen) |

---

<!-- เพิ่ม patterns ใหม่ต่อจากนี้เมื่อ encounter -->
