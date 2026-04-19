using Npgsql;
using Ryla.Core.Domain.Webhooks;
using Ryla.Core.Ports.Outbound;
using Ryla.Infrastructure.Adapters.Connections;
using Ryla.Infrastructure.Adapters.Database;
using Ryla.Infrastructure.Tests.Shared;

namespace Ryla.Infrastructure.Tests.Adapters;

[Collection(nameof(PostgresCollection))]
public sealed class ConnectionRepositoryTests : IAsyncLifetime
{
    private readonly PostgresFixture _fixture;
    private IConnectionRepository _repository = null!;

    public ConnectionRepositoryTests(PostgresFixture fixture) => _fixture = fixture;

    public async Task InitializeAsync()
    {
        await _fixture.ResetDataAsync();
        var factory = new NpgsqlConnectionFactory(_fixture.ConnectionString);
        _repository = new ConnectionRepository(factory);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    // ─── FindTenantIdByShopId tests ──────────────────────────────────────────

    [Fact]
    public async Task FindTenantIdByShopId__WhenActiveTikTokConnectionExists__ReturnsTenantId()
    {
        var tenantId = await InsertTenantAsync("TikTok Tenant");
        await InsertConnectionAsync(tenantId, "tiktok_shop",
            """{"shop_id": "TT-SHOP-001"}""", isActive: true);

        var result = await _repository.FindTenantIdByShopIdAsync(
            Platform.TikTokShop, "TT-SHOP-001");

        Assert.Equal(tenantId, result);
    }

    [Fact]
    public async Task FindTenantIdByShopId__WhenConnectionInactive__ReturnsNull()
    {
        var tenantId = await InsertTenantAsync("Inactive Tenant");
        await InsertConnectionAsync(tenantId, "tiktok_shop",
            """{"shop_id": "TT-SHOP-INACTIVE"}""", isActive: false);

        var result = await _repository.FindTenantIdByShopIdAsync(
            Platform.TikTokShop, "TT-SHOP-INACTIVE");

        Assert.Null(result);
    }

    [Fact]
    public async Task FindTenantIdByShopId__WhenPlatformMismatch__ReturnsNull()
    {
        var tenantId = await InsertTenantAsync("Platform Mismatch Tenant");
        // Insert TikTok connection แต่ query ด้วย Shopee platform
        await InsertConnectionAsync(tenantId, "tiktok_shop",
            """{"shop_id": "SHOP-MISMATCH"}""", isActive: true);

        var result = await _repository.FindTenantIdByShopIdAsync(
            Platform.Shopee, "SHOP-MISMATCH");

        Assert.Null(result);
    }

    // ─── GetLineCredentials tests ─────────────────────────────────────────────

    [Fact]
    public async Task GetLineCredentials__WhenLineConnectionExists__ReturnsCredentials()
    {
        var tenantId = await InsertTenantAsync("LINE Tenant");
        await InsertConnectionAsync(tenantId, "line_oa",
            """{"channel_access_token": "tok-abc", "target_user_id": "U12345"}""",
            isActive: true);

        var result = await _repository.GetLineCredentialsAsync(tenantId);

        Assert.NotNull(result);
        Assert.Equal("tok-abc", result.ChannelAccessToken);
        Assert.Equal("U12345", result.TargetUserId);
    }

    [Fact]
    public async Task GetLineCredentials__WhenNoLineConnection__ReturnsNull()
    {
        var tenantId = await InsertTenantAsync("No LINE Tenant");
        // มีแค่ tiktok connection ไม่มี line_oa
        await InsertConnectionAsync(tenantId, "tiktok_shop",
            """{"shop_id": "shop-001"}""", isActive: true);

        var result = await _repository.GetLineCredentialsAsync(tenantId);

        Assert.Null(result);
    }

    [Fact]
    public async Task GetLineCredentials__WhenTokenFieldMissing__ReturnsNull()
    {
        var tenantId = await InsertTenantAsync("Incomplete LINE Tenant");
        // ไม่มี channel_access_token
        await InsertConnectionAsync(tenantId, "line_oa",
            """{"target_user_id": "U12345"}""", isActive: true);

        var result = await _repository.GetLineCredentialsAsync(tenantId);

        Assert.Null(result);
    }

    // ─── helpers ─────────────────────────────────────────────────────────────

    private async Task<Guid> InsertTenantAsync(string name)
    {
        await using var conn = new NpgsqlConnection(_fixture.ConnectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(
            "INSERT INTO tenants (name) VALUES (@name) RETURNING id", conn);
        cmd.Parameters.AddWithValue("name", name);
        return (Guid)(await cmd.ExecuteScalarAsync())!;
    }

    private async Task InsertConnectionAsync(
        Guid tenantId, string platform, string credentials, bool isActive)
    {
        await using var conn = new NpgsqlConnection(_fixture.ConnectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(
            """
            INSERT INTO connections (tenant_id, platform, credentials, is_active)
            VALUES (@tenantId, @platform, @credentials::jsonb, @isActive)
            """, conn);
        cmd.Parameters.AddWithValue("tenantId", tenantId);
        cmd.Parameters.AddWithValue("platform", platform);
        cmd.Parameters.AddWithValue("credentials", credentials);
        cmd.Parameters.AddWithValue("isActive", isActive);
        await cmd.ExecuteNonQueryAsync();
    }
}
