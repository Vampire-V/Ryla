using Npgsql;
using Ryla.Infrastructure.Adapters.Database;
using Ryla.Infrastructure.Adapters.Shopee;
using Ryla.Infrastructure.Tests.Shared;

namespace Ryla.Infrastructure.Tests.Adapters.Shopee;

/// <summary>
/// Integration tests สำหรับ ShopeeTokenAdapter
/// ทดสอบ DB query methods ที่ไม่ต้องการ Supabase Vault
/// (vault methods ถูก [ExcludeFromCodeCoverage] — covered by E2E tests)
/// </summary>
[Collection(nameof(PostgresCollection))]
public sealed class ShopeeTokenAdapterTests : IAsyncLifetime
{
    private readonly PostgresFixture _fixture;
    private ShopeeTokenAdapter _sut = null!;

    public ShopeeTokenAdapterTests(PostgresFixture fixture) => _fixture = fixture;

    public async Task InitializeAsync()
    {
        await _fixture.ResetDataAsync();
        var factory = new NpgsqlConnectionFactory(_fixture.ConnectionString);
        _sut = new ShopeeTokenAdapter(factory);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    // ─── GetTenantIdByShopIdAsync ─────────────────────────────────────────────

    [Fact]
    public async Task GetTenantIdByShopIdAsync__ExistingShop__ShouldReturnTenantId()
    {
        // Arrange
        var tenantId = await _fixture.SeedTenantAsync("Shop Token Test");
        var shopId = 123456789L;
        await SeedShopeeTokenRowAsync(tenantId, shopId);

        // Act
        var result = await _sut.GetTenantIdByShopIdAsync(shopId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(tenantId, result!.Value);
    }

    [Fact]
    public async Task GetTenantIdByShopIdAsync__UnknownShop__ShouldReturnNull()
    {
        // Arrange — ไม่ seed ข้อมูลสำหรับ shop นี้
        var unknownShopId = 999999999L;

        // Act
        var result = await _sut.GetTenantIdByShopIdAsync(unknownShopId);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetTenantIdByShopIdAsync__ShopIdBelongsToDifferentTenant__ShouldReturnCorrectTenant()
    {
        // Arrange — 2 tenants, 2 shop IDs แตกต่างกัน
        var tenantA = await _fixture.SeedTenantAsync("Tenant A");
        var tenantB = await _fixture.SeedTenantAsync("Tenant B");
        await SeedShopeeTokenRowAsync(tenantA, shopId: 111L);
        await SeedShopeeTokenRowAsync(tenantB, shopId: 222L);

        // Act — ค้นหา shop ของ tenant B
        var result = await _sut.GetTenantIdByShopIdAsync(222L);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(tenantB, result!.Value);
    }

    // ─── GetTenantIdsWithTokenAsync ───────────────────────────────────────────

    [Fact]
    public async Task GetTenantIdsWithTokenAsync__MultipleTokens__ShouldReturnAll()
    {
        // Arrange — สร้าง 3 tenants แต่ละ tenant มี 1 shopee_tokens row (UNIQUE per tenant)
        var tenant1 = await _fixture.SeedTenantAsync("Token Tenant 1");
        var tenant2 = await _fixture.SeedTenantAsync("Token Tenant 2");
        var tenant3 = await _fixture.SeedTenantAsync("Token Tenant 3");

        await SeedShopeeTokenRowAsync(tenant1, shopId: 1001L);
        await SeedShopeeTokenRowAsync(tenant2, shopId: 1002L);
        await SeedShopeeTokenRowAsync(tenant3, shopId: 1003L);

        // Act
        var results = await _sut.GetTenantIdsWithTokenAsync();

        // Assert — ทั้ง 3 tenants ต้องอยู่ใน result
        Assert.Equal(3, results.Count);
        Assert.Contains(tenant1, results);
        Assert.Contains(tenant2, results);
        Assert.Contains(tenant3, results);
    }

    [Fact]
    public async Task GetTenantIdsWithTokenAsync__NoTokens__ShouldReturnEmptyList()
    {
        // Arrange — ResetData เรียบร้อยใน InitializeAsync, ไม่ seed อะไร

        // Act
        var results = await _sut.GetTenantIdsWithTokenAsync();

        // Assert
        Assert.Empty(results);
    }

    [Fact]
    public async Task GetTenantIdsWithTokenAsync__ShouldReturnTenantIds()
    {
        // Arrange
        var tenantId = await _fixture.SeedTenantAsync("Single Token Tenant");
        await SeedShopeeTokenRowAsync(tenantId, shopId: 5555L);

        // Act
        var results = await _sut.GetTenantIdsWithTokenAsync();

        // Assert
        Assert.Single(results);
        Assert.Equal(tenantId, results[0]);
    }

    // ─── Helpers ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Insert shopee_tokens row โดยตรง — ใช้ dummy UUID สำหรับ vault secret IDs
    /// (access_token_secret_id และ refresh_token_secret_id ไม่มี FK ไปยัง vault.decrypted_secrets)
    /// </summary>
    private async Task SeedShopeeTokenRowAsync(
        Guid tenantId,
        long shopId,
        DateTimeOffset? expiresAt = null)
    {
        await using var conn = new NpgsqlConnection(_fixture.ConnectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand("""
            INSERT INTO shopee_tokens
              (tenant_id, shop_id, access_token_secret_id, refresh_token_secret_id, expires_at)
            VALUES
              (@tenantId, @shopId, @accessSecretId, @refreshSecretId, @expiresAt)
            """, conn);
        cmd.Parameters.AddWithValue("tenantId", tenantId);
        cmd.Parameters.AddWithValue("shopId", shopId);
        cmd.Parameters.AddWithValue("accessSecretId", Guid.NewGuid()); // dummy vault UUID
        cmd.Parameters.AddWithValue("refreshSecretId", Guid.NewGuid()); // dummy vault UUID
        cmd.Parameters.AddWithValue("expiresAt", expiresAt ?? DateTimeOffset.UtcNow.AddHours(4));
        await cmd.ExecuteNonQueryAsync();
    }
}
