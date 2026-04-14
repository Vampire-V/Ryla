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
            WHERE id = ANY(ARRAY[@id1, @id2]::uuid[])
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
