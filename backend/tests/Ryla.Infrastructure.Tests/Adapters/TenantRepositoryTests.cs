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
