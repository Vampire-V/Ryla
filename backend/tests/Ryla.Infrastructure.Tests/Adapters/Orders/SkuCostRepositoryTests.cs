using Npgsql;
using Ryla.Core.Ports.Outbound;
using Ryla.Infrastructure.Adapters.Database;
using Ryla.Infrastructure.Adapters.Orders;
using Ryla.Infrastructure.Tests.Shared;

namespace Ryla.Infrastructure.Tests.Adapters.Orders;

[Collection(nameof(PostgresCollection))]
public sealed class SkuCostRepositoryTests : IAsyncLifetime
{
    private readonly PostgresFixture _fixture;
    private ISkuCostRepository _repository = null!;
    private Guid _tenantId;
    private Guid _otherTenantId;

    public SkuCostRepositoryTests(PostgresFixture fixture) => _fixture = fixture;

    public async Task InitializeAsync()
    {
        await _fixture.ResetDataAsync();
        var factory = new NpgsqlConnectionFactory(_fixture.ConnectionString);
        _repository = new SkuCostRepository(factory);
        _tenantId = await _fixture.SeedTenantAsync("SKU Test Business");
        _otherTenantId = await _fixture.SeedTenantAsync("Other Business");
    }

    public Task DisposeAsync() => Task.CompletedTask;

    // ─── UpsertAsync ──────────────────────────────────────────────────────────

    [Fact]
    public async Task UpsertAsync__NewSku__ShouldInsertAndReturn()
    {
        var result = await _repository.UpsertAsync(
            _tenantId, "shopee", "SKU-001", "Product A", cogs: 100m);

        Assert.NotEqual(Guid.Empty, result.Id);
        Assert.Equal(_tenantId, result.TenantId);
        Assert.Equal("shopee", result.Platform);
        Assert.Equal("SKU-001", result.ItemSku);
        Assert.Equal("Product A", result.ItemName);
        Assert.Equal(100m, result.Cogs);
    }

    [Fact]
    public async Task UpsertAsync__NewSkuWithNullName__ShouldInsertWithNullName()
    {
        var result = await _repository.UpsertAsync(
            _tenantId, "shopee", "SKU-NONAME", null, cogs: 50m);

        Assert.Null(result.ItemName);
        Assert.Equal(50m, result.Cogs);
    }

    [Fact]
    public async Task UpsertAsync__ExistingSku__ShouldUpdateCogsAndReturnUpdated()
    {
        await _repository.UpsertAsync(_tenantId, "shopee", "SKU-002", "Original Name", cogs: 80m);

        // Act: upsert ซ้ำ — update cogs
        var updated = await _repository.UpsertAsync(
            _tenantId, "shopee", "SKU-002", "Updated Name", cogs: 120m);

        Assert.Equal("Updated Name", updated.ItemName);
        Assert.Equal(120m, updated.Cogs);

        // ตรวจสอบว่า insert แค่ 1 row (ไม่ duplicate)
        var all = await _repository.GetByTenantAsync(_tenantId, "shopee");
        Assert.Single(all, s => s.ItemSku == "SKU-002");
    }

    [Fact]
    public async Task UpsertAsync__SameSkuDifferentTenants__ShouldCoexist()
    {
        await _repository.UpsertAsync(_tenantId, "shopee", "SHARED-SKU", null, cogs: 100m);
        await _repository.UpsertAsync(_otherTenantId, "shopee", "SHARED-SKU", null, cogs: 200m);

        var mine = await _repository.GetByTenantAsync(_tenantId, "shopee");
        var others = await _repository.GetByTenantAsync(_otherTenantId, "shopee");

        Assert.Equal(100m, mine.First(s => s.ItemSku == "SHARED-SKU").Cogs);
        Assert.Equal(200m, others.First(s => s.ItemSku == "SHARED-SKU").Cogs);
    }

    // ─── GetByTenantAsync ─────────────────────────────────────────────────────

    [Fact]
    public async Task GetByTenantAsync__ShouldReturnOnlyTenantRows()
    {
        await _repository.UpsertAsync(_tenantId, "shopee", "MY-SKU-1", "Mine 1", 50m);
        await _repository.UpsertAsync(_tenantId, "shopee", "MY-SKU-2", "Mine 2", 60m);
        await _repository.UpsertAsync(_otherTenantId, "shopee", "THEIR-SKU", "Theirs", 999m);

        var results = await _repository.GetByTenantAsync(_tenantId, "shopee");

        Assert.Equal(2, results.Count);
        Assert.All(results, r => Assert.Equal(_tenantId, r.TenantId));
        Assert.DoesNotContain(results, r => r.ItemSku == "THEIR-SKU");
    }

    [Fact]
    public async Task GetByTenantAsync__EmptyTenant__ShouldReturnEmptyList()
    {
        var results = await _repository.GetByTenantAsync(_tenantId, "shopee");

        Assert.Empty(results);
    }

    [Fact]
    public async Task GetByTenantAsync__ShouldReturnOrderedByItemSku()
    {
        await _repository.UpsertAsync(_tenantId, "shopee", "ZZZ-SKU", null, 10m);
        await _repository.UpsertAsync(_tenantId, "shopee", "AAA-SKU", null, 20m);
        await _repository.UpsertAsync(_tenantId, "shopee", "MMM-SKU", null, 30m);

        var results = await _repository.GetByTenantAsync(_tenantId, "shopee");

        Assert.Equal(new[] { "AAA-SKU", "MMM-SKU", "ZZZ-SKU" },
            results.Select(r => r.ItemSku));
    }

    // ─── GetCostMapAsync ──────────────────────────────────────────────────────

    [Fact]
    public async Task GetCostMapAsync__EmptyList__ShouldReturnEmptyWithoutDbCall()
    {
        // ไม่ต้อง seed — empty skus คืนเลย ไม่ query DB
        var result = await _repository.GetCostMapAsync(_tenantId, "shopee", Array.Empty<string>());

        Assert.Empty(result);
    }

    [Fact]
    public async Task GetCostMapAsync__WithSkus__ShouldReturnMap()
    {
        await _repository.UpsertAsync(_tenantId, "shopee", "COST-A", null, 100m);
        await _repository.UpsertAsync(_tenantId, "shopee", "COST-B", null, 200m);
        await _repository.UpsertAsync(_tenantId, "shopee", "COST-C", null, 300m);

        var result = await _repository.GetCostMapAsync(
            _tenantId, "shopee", ["COST-A", "COST-C"]);

        Assert.Equal(2, result.Count);
        Assert.Equal(100m, result["COST-A"]);
        Assert.Equal(300m, result["COST-C"]);
        Assert.DoesNotContain("COST-B", result.Keys);
    }

    [Fact]
    public async Task GetCostMapAsync__ShouldNotReturnOtherTenantCosts()
    {
        await _repository.UpsertAsync(_otherTenantId, "shopee", "ISOLATED-SKU", null, 999m);

        var result = await _repository.GetCostMapAsync(
            _tenantId, "shopee", ["ISOLATED-SKU"]);

        Assert.Empty(result);
    }
}
