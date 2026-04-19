# Tenant Onboarding Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Automatically provision a `tenants` row and a `profiles` row whenever a new user signs up via Supabase Auth, establishing the tenant-isolation foundation that every future feature depends on.

**Architecture:** A PostgreSQL trigger function (`handle_new_user`) fires on `auth.users` INSERT, atomically creating both rows in a single transaction. The .NET backend provides domain records (`Tenant`, `UserProfile`) in `Ryla.Core`, an `ITenantRepository` port, and a Npgsql-based adapter in `Ryla.Infrastructure` for querying tenant data. No HTTP endpoint is needed for onboarding itself -- the trigger handles provisioning; the backend provides read access for downstream features.

**Tech Stack:** PostgreSQL 15 (trigger + RLS), .NET 10 Native AOT, Npgsql 9.0.3, xUnit + NSubstitute + Testcontainers.

---

### File Map

| Action | File Path |
|--------|-----------|
| Create | `supabase/migrations/20260413000000_tenant_onboarding.sql` |
| Create | `backend/src/Ryla.Core/Domain/Tenant.cs` |
| Create | `backend/src/Ryla.Core/Domain/UserProfile.cs` |
| Create | `backend/src/Ryla.Core/Ports/Outbound/ITenantRepository.cs` |
| Create | `backend/src/Ryla.Infrastructure/Adapters/Tenants/TenantRepository.cs` |
| Modify | `backend/src/Ryla.Infrastructure/DependencyInjection.cs` |
| Create | `backend/tests/Ryla.Infrastructure.Tests/Adapters/TenantOnboardingTriggerTests.cs` |
| Create | `backend/tests/Ryla.Infrastructure.Tests/Adapters/TenantRepositoryTests.cs` |
| Create | `backend/tests/Ryla.Core.Tests/Domain/TenantTests.cs` |
| Create | `backend/tests/Ryla.Core.Tests/Domain/UserProfileTests.cs` |
| Create | `changelog/fragments/tenant-onboarding.yml` |

---

### Task 1: Database Migration -- Tenants RLS Policy + Trigger Function

**Files:**
- Create: `supabase/migrations/20260413000000_tenant_onboarding.sql`

This migration adds three things: (1) an RLS policy on `tenants` so users can only see their own tenant, (2) a `handle_new_user()` trigger function that creates a tenant + profile on signup, and (3) the trigger itself on `auth.users`.

- [ ] Step 1: Create the migration file

File: `supabase/migrations/20260413000000_tenant_onboarding.sql`

```sql
-- =============================================================
-- Tenant Onboarding: auto-provision tenant + profile on signup
-- =============================================================
-- เมื่อ user สมัครผ่าน Supabase Auth จะสร้าง tenant + profile อัตโนมัติ

-- -------------------------------------------------------------
-- 1. RLS Policy สำหรับ tenants table (ยังไม่มี)
-- user เห็นได้แค่ tenant ของตัวเอง (ผ่าน profiles lookup)
-- -------------------------------------------------------------
CREATE POLICY "tenants: tenant member access"
  ON tenants FOR ALL
  USING (
    id IN (SELECT tenant_id FROM profiles WHERE id = auth.uid())
  );

-- -------------------------------------------------------------
-- 2. Trigger function: สร้าง tenant + profile เมื่อ user ใหม่สมัคร
-- ทำงานใน transaction เดียวกับ INSERT ใน auth.users
-- -------------------------------------------------------------
CREATE OR REPLACE FUNCTION public.handle_new_user()
RETURNS TRIGGER
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = public
AS $$
DECLARE
  new_tenant_id UUID;
  user_name TEXT;
BEGIN
  -- ดึงชื่อจาก metadata หรือใช้ส่วนแรกของ email
  user_name := COALESCE(
    NEW.raw_user_meta_data ->> 'full_name',
    split_part(NEW.email, '@', 1)
  );

  -- สร้าง tenant ใหม่ (1 user = 1 tenant ตอน signup)
  INSERT INTO public.tenants (name)
  VALUES (user_name || '''s Business')
  RETURNING id INTO new_tenant_id;

  -- สร้าง profile เชื่อม user กับ tenant
  INSERT INTO public.profiles (id, tenant_id, full_name)
  VALUES (NEW.id, new_tenant_id, user_name);

  RETURN NEW;
END;
$$;

-- -------------------------------------------------------------
-- 3. Trigger: fire หลัง INSERT ใน auth.users
-- FOR EACH ROW: ทำงานทุกครั้งที่มี user ใหม่
-- -------------------------------------------------------------
CREATE TRIGGER on_auth_user_created
  AFTER INSERT ON auth.users
  FOR EACH ROW
  EXECUTE FUNCTION public.handle_new_user();
```

- [ ] Step 2: Verify migration syntax is valid by dry-running against test container

Run: `cd /Users/pipat/Documents/MiniProject/Ryla && dotnet test backend/tests/Ryla.Infrastructure.Tests --filter "Database__AfterMigrationsApplied__ShouldHaveTenantsTable" --verbosity normal`

Expected: PASS -- the existing smoke test should still pass, confirming the new migration applies cleanly on top of the init migration.

- [ ] Step 3: Commit

```
git add supabase/migrations/20260413000000_tenant_onboarding.sql
git commit -m "feat(supabase): add tenant onboarding trigger and tenants RLS policy

เพิ่ม handle_new_user() trigger function ที่สร้าง tenant + profile อัตโนมัติ
เมื่อ user สมัครผ่าน Supabase Auth และเพิ่ม RLS policy บน tenants table"
```

---

### Task 2: Domain Records -- Tenant and UserProfile in Core

**Files:**
- Create: `backend/src/Ryla.Core/Domain/Tenant.cs`
- Create: `backend/src/Ryla.Core/Domain/UserProfile.cs`
- Create: `backend/tests/Ryla.Core.Tests/Domain/TenantTests.cs`
- Create: `backend/tests/Ryla.Core.Tests/Domain/UserProfileTests.cs`

Domain records are pure C# with zero external dependencies. They represent the data shape returned by repository queries.

- [ ] Step 1: Write failing tests for Tenant record

File: `backend/tests/Ryla.Core.Tests/Domain/TenantTests.cs`

```csharp
using Ryla.Core.Domain;

namespace Ryla.Core.Tests.Domain;

public sealed class TenantTests
{
    [Fact]
    public void Tenant__WhenCreated__ShouldStoreAllProperties()
    {
        var id = Guid.NewGuid();
        var name = "Test Business";
        var createdAt = DateTimeOffset.UtcNow;

        var tenant = new Tenant(id, name, createdAt);

        Assert.Equal(id, tenant.Id);
        Assert.Equal(name, tenant.Name);
        Assert.Equal(createdAt, tenant.CreatedAt);
    }

    [Fact]
    public void Tenant__WhenComparedWithSameValues__ShouldBeEqual()
    {
        var id = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        var tenant1 = new Tenant(id, "Biz", now);
        var tenant2 = new Tenant(id, "Biz", now);

        Assert.Equal(tenant1, tenant2);
    }
}
```

- [ ] Step 2: Write failing tests for UserProfile record

File: `backend/tests/Ryla.Core.Tests/Domain/UserProfileTests.cs`

```csharp
using Ryla.Core.Domain;

namespace Ryla.Core.Tests.Domain;

public sealed class UserProfileTests
{
    [Fact]
    public void UserProfile__WhenCreated__ShouldStoreAllProperties()
    {
        var id = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var fullName = "Somchai";
        var createdAt = DateTimeOffset.UtcNow;

        var profile = new UserProfile(id, tenantId, fullName, createdAt);

        Assert.Equal(id, profile.Id);
        Assert.Equal(tenantId, profile.TenantId);
        Assert.Equal(fullName, profile.FullName);
        Assert.Equal(createdAt, profile.CreatedAt);
    }

    [Fact]
    public void UserProfile__WhenFullNameIsNull__ShouldAllowNull()
    {
        var profile = new UserProfile(
            Guid.NewGuid(),
            Guid.NewGuid(),
            null,
            DateTimeOffset.UtcNow);

        Assert.Null(profile.FullName);
    }
}
```

- [ ] Step 3: Run tests to verify they fail (types don't exist yet)

Run: `cd /Users/pipat/Documents/MiniProject/Ryla && dotnet test backend/tests/Ryla.Core.Tests --verbosity normal`

Expected: FAIL -- `error CS0234: The type or namespace name 'Domain' does not exist in the namespace 'Ryla.Core'`

- [ ] Step 4: Create Tenant record

File: `backend/src/Ryla.Core/Domain/Tenant.cs`

```csharp
namespace Ryla.Core.Domain;

/// <summary>
/// Tenant = หนึ่งธุรกิจ SME ไทย
/// สร้างอัตโนมัติเมื่อ user สมัครใหม่ผ่าน Supabase Auth trigger
/// </summary>
public sealed record Tenant(
    Guid Id,
    string Name,
    DateTimeOffset CreatedAt);
```

- [ ] Step 5: Create UserProfile record

File: `backend/src/Ryla.Core/Domain/UserProfile.cs`

```csharp
namespace Ryla.Core.Domain;

/// <summary>
/// Profile เชื่อม auth.users.id กับ tenant
/// full_name อาจเป็น null ถ้า user ไม่ได้ให้ข้อมูลตอนสมัคร
/// </summary>
public sealed record UserProfile(
    Guid Id,
    Guid TenantId,
    string? FullName,
    DateTimeOffset CreatedAt);
```

- [ ] Step 6: Run tests to verify they pass

Run: `cd /Users/pipat/Documents/MiniProject/Ryla && dotnet test backend/tests/Ryla.Core.Tests --verbosity normal`

Expected: PASS -- all 4 tests green.

- [ ] Step 7: Commit

```
git add backend/src/Ryla.Core/Domain/Tenant.cs backend/src/Ryla.Core/Domain/UserProfile.cs backend/tests/Ryla.Core.Tests/Domain/TenantTests.cs backend/tests/Ryla.Core.Tests/Domain/UserProfileTests.cs
git commit -m "feat(core): add Tenant and UserProfile domain records

เพิ่ม domain records สำหรับ tenant isolation foundation
Tenant = หนึ่งธุรกิจ, UserProfile = เชื่อม user กับ tenant"
```

---

### Task 3: Repository Port -- ITenantRepository in Core

**Files:**
- Create: `backend/src/Ryla.Core/Ports/Outbound/ITenantRepository.cs`

This port defines the contract for reading tenant data. Write operations happen via the PostgreSQL trigger, so the port only needs read methods. The interface lives in Core with zero external dependencies.

- [ ] Step 1: Create ITenantRepository interface

File: `backend/src/Ryla.Core/Ports/Outbound/ITenantRepository.cs`

```csharp
using Ryla.Core.Domain;

namespace Ryla.Core.Ports.Outbound;

/// <summary>
/// Port สำหรับอ่านข้อมูล tenant
/// Write operations ทำผ่าน Supabase Auth trigger (handle_new_user)
/// </summary>
public interface ITenantRepository
{
    /// <summary>ดึง tenant จาก ID</summary>
    Task<Tenant?> GetByIdAsync(Guid tenantId, CancellationToken cancellationToken = default);

    /// <summary>ดึง tenant ที่ user เป็นสมาชิก (ผ่าน profiles table)</summary>
    Task<Tenant?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>ดึง profile ของ user</summary>
    Task<UserProfile?> GetProfileByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
}
```

- [ ] Step 2: Verify Core project still compiles with no external dependencies

Run: `cd /Users/pipat/Documents/MiniProject/Ryla && dotnet build backend/src/Ryla.Core/Ryla.Core.csproj --verbosity normal`

Expected: PASS -- Build succeeded with 0 warnings. No PackageReference elements in Ryla.Core.csproj.

- [ ] Step 3: Commit

```
git add backend/src/Ryla.Core/Ports/Outbound/ITenantRepository.cs
git commit -m "feat(core): add ITenantRepository outbound port

เพิ่ม interface สำหรับอ่านข้อมูล tenant/profile
write operations ทำผ่าน PostgreSQL trigger ไม่ต้องผ่าน .NET"
```

---

### Task 4: Integration Tests -- Trigger Behavior via Testcontainers

**Files:**
- Create: `backend/tests/Ryla.Infrastructure.Tests/Adapters/TenantOnboardingTriggerTests.cs`

These tests simulate Supabase Auth signup by inserting directly into `auth.users` and verifying the trigger creates the expected `tenants` and `profiles` rows.

- [ ] Step 1: Create trigger integration tests

File: `backend/tests/Ryla.Infrastructure.Tests/Adapters/TenantOnboardingTriggerTests.cs`

```csharp
using Npgsql;
using Ryla.Infrastructure.Tests.Shared;

namespace Ryla.Infrastructure.Tests.Adapters;

[Collection(nameof(PostgresCollection))]
public sealed class TenantOnboardingTriggerTests : IAsyncLifetime
{
    private readonly PostgresFixture _fixture;

    public TenantOnboardingTriggerTests(PostgresFixture fixture) => _fixture = fixture;

    public Task InitializeAsync() => _fixture.ResetDataAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task HandleNewUser__WhenUserInserted__ShouldCreateTenantRow()
    {
        var userId = Guid.NewGuid();
        var email = "somchai@example.com";

        await InsertAuthUserAsync(userId, email);

        await using var conn = new NpgsqlConnection(_fixture.ConnectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(
            "SELECT COUNT(*) FROM tenants WHERE id = (SELECT tenant_id FROM profiles WHERE id = @id)",
            conn);
        cmd.Parameters.AddWithValue("id", userId);

        var count = (long)(await cmd.ExecuteScalarAsync())!;
        Assert.Equal(1L, count);
    }

    [Fact]
    public async Task HandleNewUser__WhenUserInserted__ShouldCreateProfileRow()
    {
        var userId = Guid.NewGuid();
        var email = "somying@example.com";

        await InsertAuthUserAsync(userId, email);

        await using var conn = new NpgsqlConnection(_fixture.ConnectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(
            "SELECT tenant_id, full_name FROM profiles WHERE id = @id",
            conn);
        cmd.Parameters.AddWithValue("id", userId);

        await using var reader = await cmd.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync(), "Profile row should exist");

        var tenantId = reader.GetGuid(0);
        var fullName = reader.GetString(1);

        Assert.NotEqual(Guid.Empty, tenantId);
        Assert.Equal("somying", fullName); // split_part('somying@example.com', '@', 1)
    }

    [Fact]
    public async Task HandleNewUser__WhenUserInserted__ShouldDeriveTenantNameFromEmail()
    {
        var userId = Guid.NewGuid();
        var email = "kittisak@shop.co.th";

        await InsertAuthUserAsync(userId, email);

        await using var conn = new NpgsqlConnection(_fixture.ConnectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(
            """
            SELECT t.name FROM tenants t
            INNER JOIN profiles p ON p.tenant_id = t.id
            WHERE p.id = @id
            """,
            conn);
        cmd.Parameters.AddWithValue("id", userId);

        var tenantName = (string)(await cmd.ExecuteScalarAsync())!;
        Assert.Equal("kittisak's Business", tenantName);
    }

    [Fact]
    public async Task HandleNewUser__WhenUserHasFullNameMetadata__ShouldUseFullName()
    {
        var userId = Guid.NewGuid();
        var email = "test@example.com";
        var fullName = "Somchai Jaidee";

        await InsertAuthUserWithMetadataAsync(userId, email, fullName);

        await using var conn = new NpgsqlConnection(_fixture.ConnectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(
            "SELECT full_name FROM profiles WHERE id = @id",
            conn);
        cmd.Parameters.AddWithValue("id", userId);

        var storedName = (string)(await cmd.ExecuteScalarAsync())!;
        Assert.Equal("Somchai Jaidee", storedName);
    }

    [Fact]
    public async Task HandleNewUser__WhenUserHasFullNameMetadata__ShouldDeriveTenantNameFromFullName()
    {
        var userId = Guid.NewGuid();
        var email = "test2@example.com";
        var fullName = "Somchai Jaidee";

        await InsertAuthUserWithMetadataAsync(userId, email, fullName);

        await using var conn = new NpgsqlConnection(_fixture.ConnectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(
            """
            SELECT t.name FROM tenants t
            INNER JOIN profiles p ON p.tenant_id = t.id
            WHERE p.id = @id
            """,
            conn);
        cmd.Parameters.AddWithValue("id", userId);

        var tenantName = (string)(await cmd.ExecuteScalarAsync())!;
        Assert.Equal("Somchai Jaidee's Business", tenantName);
    }

    [Fact]
    public async Task HandleNewUser__WhenTwoUsersSignup__ShouldCreateSeparateTenants()
    {
        var userId1 = Guid.NewGuid();
        var userId2 = Guid.NewGuid();

        await InsertAuthUserAsync(userId1, "user1@example.com");
        await InsertAuthUserAsync(userId2, "user2@example.com");

        await using var conn = new NpgsqlConnection(_fixture.ConnectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(
            """
            SELECT COUNT(DISTINCT tenant_id) FROM profiles
            WHERE id IN (@id1, @id2)
            """,
            conn);
        cmd.Parameters.AddWithValue("id1", userId1);
        cmd.Parameters.AddWithValue("id2", userId2);

        var distinctTenants = (long)(await cmd.ExecuteScalarAsync())!;
        Assert.Equal(2L, distinctTenants);
    }

    // ── helpers ──────────────────────────────────────────────────────

    private async Task InsertAuthUserAsync(Guid userId, string email)
    {
        await using var conn = new NpgsqlConnection(_fixture.ConnectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(
            "INSERT INTO auth.users (id, email) VALUES (@id, @email)",
            conn);
        cmd.Parameters.AddWithValue("id", userId);
        cmd.Parameters.AddWithValue("email", email);
        await cmd.ExecuteNonQueryAsync();
    }

    private async Task InsertAuthUserWithMetadataAsync(Guid userId, string email, string fullName)
    {
        await using var conn = new NpgsqlConnection(_fixture.ConnectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(
            """
            INSERT INTO auth.users (id, email, raw_user_meta_data)
            VALUES (@id, @email, @meta::jsonb)
            """,
            conn);
        cmd.Parameters.AddWithValue("id", userId);
        cmd.Parameters.AddWithValue("email", email);
        cmd.Parameters.AddWithValue("meta", $"{{\"full_name\": \"{fullName}\"}}");
        await cmd.ExecuteNonQueryAsync();
    }
}
```

- [ ] Step 2: Update `auth.users` mock table in `PostgresFixture` to support `raw_user_meta_data`

The existing `PostgresFixture.ApplySchemaAsync()` creates `auth.users` with only `id` and `email` columns. The trigger references `raw_user_meta_data`, so we must add that column.

Modify file: `backend/tests/Ryla.Infrastructure.Tests/Shared/PostgresFixture.cs`

Change the mock auth schema SQL block from:

```csharp
        await ExecuteSqlAsync(connection, """
            CREATE SCHEMA IF NOT EXISTS auth;
            CREATE TABLE IF NOT EXISTS auth.users (
                id   UUID PRIMARY KEY DEFAULT gen_random_uuid(),
                email TEXT UNIQUE
            );
            -- ฟังก์ชัน auth.uid() สำหรับ RLS policies
            CREATE OR REPLACE FUNCTION auth.uid() RETURNS UUID
                LANGUAGE sql STABLE AS $$SELECT '00000000-0000-0000-0000-000000000000'::uuid$$;
            """);
```

to:

```csharp
        await ExecuteSqlAsync(connection, """
            CREATE SCHEMA IF NOT EXISTS auth;
            CREATE TABLE IF NOT EXISTS auth.users (
                id   UUID PRIMARY KEY DEFAULT gen_random_uuid(),
                email TEXT UNIQUE,
                raw_user_meta_data JSONB DEFAULT '{}'::jsonb
            );
            -- ฟังก์ชัน auth.uid() สำหรับ RLS policies
            CREATE OR REPLACE FUNCTION auth.uid() RETURNS UUID
                LANGUAGE sql STABLE AS $$SELECT '00000000-0000-0000-0000-000000000000'::uuid$$;
            """);
```

- [ ] Step 3: Run the trigger tests (they should pass because Task 1 migration exists)

Run: `cd /Users/pipat/Documents/MiniProject/Ryla && dotnet test backend/tests/Ryla.Infrastructure.Tests --filter "TenantOnboardingTriggerTests" --verbosity normal`

Expected: PASS -- all 6 trigger tests green. The migration from Task 1 is auto-applied by `PostgresFixture.ApplySchemaAsync()`.

- [ ] Step 4: Also verify existing smoke tests still pass

Run: `cd /Users/pipat/Documents/MiniProject/Ryla && dotnet test backend/tests/Ryla.Infrastructure.Tests --filter "DatabaseSmokeTests" --verbosity normal`

Expected: PASS -- all 4 existing smoke tests still green.

- [ ] Step 5: Commit

```
git add backend/tests/Ryla.Infrastructure.Tests/Adapters/TenantOnboardingTriggerTests.cs backend/tests/Ryla.Infrastructure.Tests/Shared/PostgresFixture.cs
git commit -m "test(infra): add integration tests for tenant onboarding trigger

ทดสอบ trigger handle_new_user() ว่าสร้าง tenant + profile ถูกต้อง
รวมถึง edge cases: metadata full_name, email fallback, tenant isolation"
```

---

### Task 5: TenantRepository -- Npgsql Adapter Implementation

**Files:**
- Create: `backend/tests/Ryla.Infrastructure.Tests/Adapters/TenantRepositoryTests.cs`
- Create: `backend/src/Ryla.Infrastructure/Adapters/Tenants/TenantRepository.cs`

TDD flow: write integration tests first, then implement the adapter.

- [ ] Step 1: Write failing integration tests for TenantRepository

File: `backend/tests/Ryla.Infrastructure.Tests/Adapters/TenantRepositoryTests.cs`

```csharp
using Npgsql;
using Ryla.Core.Domain;
using Ryla.Core.Ports.Outbound;
using Ryla.Infrastructure.Adapters.Database;
using Ryla.Infrastructure.Adapters.Tenants;
using Ryla.Infrastructure.Tests.Shared;

namespace Ryla.Infrastructure.Tests.Adapters;

[Collection(nameof(PostgresCollection))]
public sealed class TenantRepositoryTests : IAsyncLifetime
{
    private readonly PostgresFixture _fixture;
    private ITenantRepository _repository = null!;

    public TenantRepositoryTests(PostgresFixture fixture) => _fixture = fixture;

    public async Task InitializeAsync()
    {
        await _fixture.ResetDataAsync();
        var factory = new NpgsqlConnectionFactory(_fixture.ConnectionString);
        _repository = new TenantRepository(factory);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task GetByIdAsync__WhenTenantExists__ShouldReturnTenant()
    {
        var userId = Guid.NewGuid();
        await InsertAuthUserAsync(userId, "get-by-id@example.com");

        // ดึง tenant_id ที่ trigger สร้าง
        var tenantId = await GetTenantIdForUserAsync(userId);

        var result = await _repository.GetByIdAsync(tenantId);

        Assert.NotNull(result);
        Assert.Equal(tenantId, result.Id);
        Assert.Equal("get-by-id's Business", result.Name);
    }

    [Fact]
    public async Task GetByIdAsync__WhenTenantDoesNotExist__ShouldReturnNull()
    {
        var result = await _repository.GetByIdAsync(Guid.NewGuid());

        Assert.Null(result);
    }

    [Fact]
    public async Task GetByUserIdAsync__WhenUserExists__ShouldReturnTenant()
    {
        var userId = Guid.NewGuid();
        await InsertAuthUserAsync(userId, "by-user@example.com");

        var result = await _repository.GetByUserIdAsync(userId);

        Assert.NotNull(result);
        Assert.Equal("by-user's Business", result.Name);
    }

    [Fact]
    public async Task GetByUserIdAsync__WhenUserDoesNotExist__ShouldReturnNull()
    {
        var result = await _repository.GetByUserIdAsync(Guid.NewGuid());

        Assert.Null(result);
    }

    [Fact]
    public async Task GetProfileByUserIdAsync__WhenProfileExists__ShouldReturnProfile()
    {
        var userId = Guid.NewGuid();
        await InsertAuthUserAsync(userId, "profile@example.com");

        var result = await _repository.GetProfileByUserIdAsync(userId);

        Assert.NotNull(result);
        Assert.Equal(userId, result.Id);
        Assert.Equal("profile", result.FullName);
        Assert.NotEqual(Guid.Empty, result.TenantId);
    }

    [Fact]
    public async Task GetProfileByUserIdAsync__WhenProfileDoesNotExist__ShouldReturnNull()
    {
        var result = await _repository.GetProfileByUserIdAsync(Guid.NewGuid());

        Assert.Null(result);
    }

    // ── helpers ──────────────────────────────────────────────────────

    private async Task InsertAuthUserAsync(Guid userId, string email)
    {
        await using var conn = new NpgsqlConnection(_fixture.ConnectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(
            "INSERT INTO auth.users (id, email) VALUES (@id, @email)",
            conn);
        cmd.Parameters.AddWithValue("id", userId);
        cmd.Parameters.AddWithValue("email", email);
        await cmd.ExecuteNonQueryAsync();
    }

    private async Task<Guid> GetTenantIdForUserAsync(Guid userId)
    {
        await using var conn = new NpgsqlConnection(_fixture.ConnectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(
            "SELECT tenant_id FROM profiles WHERE id = @id",
            conn);
        cmd.Parameters.AddWithValue("id", userId);
        return (Guid)(await cmd.ExecuteScalarAsync())!;
    }
}
```

- [ ] Step 2: Run tests to verify they fail (TenantRepository class doesn't exist yet)

Run: `cd /Users/pipat/Documents/MiniProject/Ryla && dotnet test backend/tests/Ryla.Infrastructure.Tests --filter "TenantRepositoryTests" --verbosity normal`

Expected: FAIL -- `error CS0246: The type or namespace name 'TenantRepository' could not be found`

- [ ] Step 3: Implement TenantRepository

File: `backend/src/Ryla.Infrastructure/Adapters/Tenants/TenantRepository.cs`

```csharp
using Ryla.Core.Domain;
using Ryla.Core.Ports.Outbound;
using Ryla.Infrastructure.Adapters.Database;

namespace Ryla.Infrastructure.Adapters.Tenants;

/// <summary>
/// Npgsql implementation ของ ITenantRepository
/// อ่านข้อมูล tenant/profile จาก PostgreSQL
/// Write operations ทำผ่าน Supabase Auth trigger
/// </summary>
internal sealed class TenantRepository : ITenantRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public TenantRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<Tenant?> GetByIdAsync(
        Guid tenantId, CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.CreateAsync(cancellationToken);
        await using var cmd = connection.CreateCommand();

        cmd.CommandText = "SELECT id, name, created_at FROM tenants WHERE id = @id";
        cmd.Parameters.AddWithValue("id", tenantId);

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            return null;

        return new Tenant(
            reader.GetGuid(0),
            reader.GetString(1),
            reader.GetFieldValue<DateTimeOffset>(2));
    }

    public async Task<Tenant?> GetByUserIdAsync(
        Guid userId, CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.CreateAsync(cancellationToken);
        await using var cmd = connection.CreateCommand();

        cmd.CommandText = """
            SELECT t.id, t.name, t.created_at
            FROM tenants t
            INNER JOIN profiles p ON p.tenant_id = t.id
            WHERE p.id = @userId
            """;
        cmd.Parameters.AddWithValue("userId", userId);

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            return null;

        return new Tenant(
            reader.GetGuid(0),
            reader.GetString(1),
            reader.GetFieldValue<DateTimeOffset>(2));
    }

    public async Task<UserProfile?> GetProfileByUserIdAsync(
        Guid userId, CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.CreateAsync(cancellationToken);
        await using var cmd = connection.CreateCommand();

        cmd.CommandText = "SELECT id, tenant_id, full_name, created_at FROM profiles WHERE id = @userId";
        cmd.Parameters.AddWithValue("userId", userId);

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            return null;

        return new UserProfile(
            reader.GetGuid(0),
            reader.GetGuid(1),
            reader.IsDBNull(2) ? null : reader.GetString(2),
            reader.GetFieldValue<DateTimeOffset>(3));
    }
}
```

- [ ] Step 4: Run tests to verify they pass

Run: `cd /Users/pipat/Documents/MiniProject/Ryla && dotnet test backend/tests/Ryla.Infrastructure.Tests --filter "TenantRepositoryTests" --verbosity normal`

Expected: PASS -- all 6 repository tests green.

- [ ] Step 5: Commit

```
git add backend/src/Ryla.Infrastructure/Adapters/Tenants/TenantRepository.cs backend/tests/Ryla.Infrastructure.Tests/Adapters/TenantRepositoryTests.cs
git commit -m "feat(infra): add TenantRepository Npgsql adapter

implement ITenantRepository สำหรับอ่าน tenant/profile จาก PostgreSQL
ใช้ Npgsql raw SQL (AOT-safe, ไม่มี ORM reflection)"
```

---

### Task 6: DI Wiring -- Register TenantRepository

**Files:**
- Modify: `backend/src/Ryla.Infrastructure/DependencyInjection.cs`

- [ ] Step 1: Register TenantRepository in DI container

Modify file: `backend/src/Ryla.Infrastructure/DependencyInjection.cs`

Add the following using statements at the top:

```csharp
using Ryla.Core.Ports.Outbound;
using Ryla.Infrastructure.Adapters.Tenants;
```

Replace the TODO comment block:

```csharp
        // TODO: ลงทะเบียน adapters ที่นี่เมื่อสร้าง (explicit, ห้ามใช้ assembly scanning)
        // ตัวอย่าง: services.AddScoped<INotificationPort, LineOaAdapter>();
        // ตัวอย่าง: services.AddScoped<ITenantRepository, NpgsqlTenantRepository>();
```

with:

```csharp
        // Tenant repository: Scoped เพราะแต่ละ request ควรได้ connection ใหม่
        services.AddScoped<ITenantRepository, TenantRepository>();
```

The complete file should look like:

```csharp
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Ryla.Core.Ports.Outbound;
using Ryla.Infrastructure.Adapters.Database;
using Ryla.Infrastructure.Adapters.Tenants;

namespace Ryla.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddRylaInfrastructureServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // ─── Database ───────────────────────────────────────────────────────────
        var connectionString = configuration.GetConnectionString("Supabase")
            ?? throw new InvalidOperationException(
                "ConnectionStrings:Supabase is not configured. " +
                "Set it in appsettings.Development.json or via dotnet user-secrets.");

        // Singleton: NpgsqlDataSource manages the connection pool — สร้างครั้งเดียวต่อ app lifetime
        services.AddSingleton<IDbConnectionFactory>(
            new NpgsqlConnectionFactory(connectionString));

        // ─── Adapters ────────────────────────────────────────────────────────────
        // Tenant repository: Scoped เพราะแต่ละ request ควรได้ connection ใหม่
        services.AddScoped<ITenantRepository, TenantRepository>();

        return services;
    }
}
```

- [ ] Step 2: Verify the full solution builds

Run: `cd /Users/pipat/Documents/MiniProject/Ryla && dotnet build backend/Ryla.slnx --verbosity normal`

Expected: PASS -- Build succeeded with 0 errors.

- [ ] Step 3: Verify all tests still pass

Run: `cd /Users/pipat/Documents/MiniProject/Ryla && dotnet test backend/Ryla.slnx --verbosity normal`

Expected: PASS -- all tests green (Core domain tests + Infrastructure smoke tests + trigger tests + repository tests + API health tests).

- [ ] Step 4: Commit

```
git add backend/src/Ryla.Infrastructure/DependencyInjection.cs
git commit -m "chore(infra): register TenantRepository in DI container

ลงทะเบียน ITenantRepository -> TenantRepository แบบ explicit (AOT-safe)
ใช้ AddScoped เพราะแต่ละ request ควรได้ connection ใหม่"
```

---

### Task 7: AOT Validation

**Files:**
- No new files. Validation only.

- [ ] Step 1: Run AOT publish and check for trim/AOT warnings

Run: `cd /Users/pipat/Documents/MiniProject/Ryla && dotnet publish backend/src/Ryla.Api/Ryla.Api.csproj -r linux-x64 --self-contained -c Release --verbosity normal 2>&1 | grep -E "(warning IL|warning NETSDK|error|Build succeeded)" || echo "Check output above for warnings"`

Expected: `Build succeeded` with 0 IL20xx/IL26xx trim warnings related to Ryla code. Third-party warnings (if any) from Npgsql or ASP.NET are acceptable and already known.

- [ ] Step 2: Verify no new types need JsonSerializable registration

The Tenant Onboarding feature has no HTTP endpoints -- provisioning happens via PostgreSQL trigger, and `TenantRepository` is consumed internally by future use cases. Therefore, no new `[JsonSerializable]` entries are needed in `RylaJsonContext` for this task.

Verification: grep to confirm no new request/response records were added to the API layer:

Run: `cd /Users/pipat/Documents/MiniProject/Ryla && grep -r "record.*Request\|record.*Response" backend/src/Ryla.Api/ --include="*.cs"`

Expected: Only `HealthResponse` appears -- no new types to register.

- [ ] Step 3: Commit (no changes -- validation only, skip commit if clean)

Run: `git status`

Expected: `nothing to commit, working tree clean`

---

### Task 8: Changelog Fragment

**Files:**
- Create: `changelog/fragments/tenant-onboarding.yml`

- [ ] Step 1: Create changelog fragment

File: `changelog/fragments/tenant-onboarding.yml`

```yaml
pr: 0
type: feat
scope: "supabase"
title: "Auto-provision tenant and profile on user signup"
description: |
  เพิ่ม PostgreSQL trigger function (handle_new_user) ที่สร้าง tenant row
  และ profile row อัตโนมัติเมื่อ user สมัครผ่าน Supabase Auth พร้อม RLS
  policy บน tenants table และ ITenantRepository adapter สำหรับ .NET backend
breaking: false
migration: true
```

- [ ] Step 2: Commit

```
git add changelog/fragments/tenant-onboarding.yml
git commit -m "docs: add changelog fragment for tenant onboarding feature

เพิ่ม changelog fragment สำหรับ auto-provisioning tenant + profile on signup"
```

---

### Self-Review Checklist

| Check | Status |
|-------|--------|
| **Spec coverage:** Tenants row auto-created on signup | Task 1 (trigger) + Task 4 (test) |
| **Spec coverage:** Profiles row linking user to tenant | Task 1 (trigger) + Task 4 (test) |
| **Spec coverage:** Tenants RLS policy | Task 1 (migration) |
| **Spec coverage:** Domain records in Core | Task 2 |
| **Spec coverage:** Repository port in Core | Task 3 |
| **Spec coverage:** Repository adapter in Infrastructure | Task 5 |
| **Spec coverage:** DI wiring | Task 6 |
| **Spec coverage:** AOT validation | Task 7 |
| **Placeholder scan:** No TBD/TODO/placeholder patterns | CLEAN |
| **Type consistency:** `Tenant` record used in Core, Infra, Tests | CONSISTENT |
| **Type consistency:** `UserProfile` record used in Core, Infra, Tests | CONSISTENT |
| **Type consistency:** `ITenantRepository` interface in Core, implemented in Infra | CONSISTENT |
| **AOT constraint:** No reflection, no dynamic, source-gen JSON | COMPLIANT |
| **Core zero deps:** No NuGet added to Ryla.Core.csproj | COMPLIANT |
| **Test naming:** `{Method}__{Scenario}__{Expected}` convention | COMPLIANT |
| **Mock library:** NSubstitute only (not used in this feature -- all integration tests) | COMPLIANT |
| **RLS:** Policy added to tenants table | COMPLIANT |
| **SECURITY DEFINER** on trigger function with explicit `SET search_path` | COMPLIANT |
