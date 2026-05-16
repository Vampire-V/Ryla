using Npgsql;
using Ryla.Core.Domain.Orders;
using Ryla.Core.Ports.Outbound;
using Ryla.Infrastructure.Adapters.Database;
using Ryla.Infrastructure.Adapters.Orders;
using Ryla.Infrastructure.Tests.Shared;

namespace Ryla.Infrastructure.Tests.Adapters.Orders;

[Collection(nameof(PostgresCollection))]
public sealed class OrderRepositoryTests : IAsyncLifetime
{
    private readonly PostgresFixture _fixture;
    private IOrderRepository _repository = null!;
    private Guid _tenantId;

    public OrderRepositoryTests(PostgresFixture fixture) => _fixture = fixture;

    public async Task InitializeAsync()
    {
        await _fixture.ResetDataAsync();
        var factory = new NpgsqlConnectionFactory(_fixture.ConnectionString);
        _repository = new OrderRepository(factory);
        _tenantId = await _fixture.SeedTenantAsync("Order Test Business");
    }

    public Task DisposeAsync() => Task.CompletedTask;

    // ─── UpsertFromWebhookAsync ───────────────────────────────────────────────

    [Fact]
    public async Task UpsertFromWebhookAsync__NewOrder__ShouldInsertRow()
    {
        await _repository.UpsertFromWebhookAsync(_tenantId, "shopee", "ORDER-001", "READY_TO_SHIP");

        var row = await GetOrderRowAsync("ORDER-001");
        Assert.NotNull(row);
        Assert.Equal("READY_TO_SHIP", row!.Value.Status);
        Assert.Equal("pending", row!.Value.SyncStatus);
    }

    [Fact]
    public async Task UpsertFromWebhookAsync__ExistingOrder__ShouldUpdateStatus()
    {
        await _repository.UpsertFromWebhookAsync(_tenantId, "shopee", "ORDER-002", "READY_TO_SHIP");
        await _repository.UpsertFromWebhookAsync(_tenantId, "shopee", "ORDER-002", "COMPLETED");

        var row = await GetOrderRowAsync("ORDER-002");
        Assert.Equal("COMPLETED", row!.Value.Status);
    }

    [Fact]
    public async Task UpsertFromWebhookAsync__WebhookAfterSync__ShouldNotOverwriteFinancials()
    {
        // Arrange: insert completed+synced order โดยตรงผ่าน SQL
        var orderId = await InsertSyncedOrderAsync("ORDER-003", revenue: 999.99m, grossMargin: 100m);

        // Act: webhook มาอีกครั้ง (status update) → ไม่ควร overwrite financial fields
        await _repository.UpsertFromWebhookAsync(_tenantId, "shopee", "ORDER-003", "COMPLETED");

        // Assert: financial ยังอยู่ครบ
        var row = await GetOrderRowAsync("ORDER-003");
        Assert.NotNull(row);
        Assert.Equal(999.99m, row!.Value.Revenue);
        Assert.Equal(100m, row!.Value.GrossMargin);
        Assert.Equal("synced", row!.Value.SyncStatus);
    }

    // ─── GetPendingCompletedOrdersAsync ──────────────────────────────────────

    [Fact]
    public async Task GetPendingCompletedOrdersAsync__ShouldReturnOnlyPendingCompleted()
    {
        // insert orders ต่างสถานะ
        await _repository.UpsertFromWebhookAsync(_tenantId, "shopee", "COMPLETED-PENDING", "COMPLETED");
        await _repository.UpsertFromWebhookAsync(_tenantId, "shopee", "READY-PENDING", "READY_TO_SHIP");
        await InsertSyncedOrderAsync("COMPLETED-SYNCED", revenue: 500m);

        var results = await _repository.GetPendingCompletedOrdersAsync(_tenantId);

        Assert.Single(results);
        Assert.Equal("COMPLETED-PENDING", results[0].OrderSn);
    }

    [Fact]
    public async Task GetPendingCompletedOrdersAsync__ShouldRespectLimit()
    {
        for (var i = 0; i < 5; i++)
            await _repository.UpsertFromWebhookAsync(_tenantId, "shopee", $"PENDING-{i:D2}", "COMPLETED");

        var results = await _repository.GetPendingCompletedOrdersAsync(_tenantId, limit: 3);

        Assert.Equal(3, results.Count);
    }

    // ─── UpdateSyncedAsync ────────────────────────────────────────────────────

    [Fact]
    public async Task UpdateSyncedAsync__WithItems__ShouldUpdateOrderAndInsertItems()
    {
        // Arrange
        await _repository.UpsertFromWebhookAsync(_tenantId, "shopee", "SYNC-01", "COMPLETED");
        var pendingOrders = await _repository.GetPendingCompletedOrdersAsync(_tenantId);
        var orderId = pendingOrders[0].OrderId;

        var detail = MakeOrderDetail("SYNC-01");
        var items = new List<OrderItem>
        {
            new(Guid.NewGuid(), orderId, _tenantId, "SKU-A", null, "Item A", 2, 100m),
            new(Guid.NewGuid(), orderId, _tenantId, "SKU-B", "MODEL-1", "Item B", 1, 200m),
        };

        // Act
        await _repository.UpdateSyncedAsync(orderId, detail, grossMargin: 150m, netMargin: 80m, items);

        // Assert: order updated
        var row = await GetOrderRowAsync("SYNC-01");
        Assert.Equal("synced", row!.Value.SyncStatus);
        Assert.Equal(1500m, row!.Value.Revenue); // EscrowTotalAmount
        Assert.Equal(150m, row!.Value.GrossMargin);
        Assert.Equal(80m, row!.Value.NetMargin);

        // Assert: items inserted
        var itemCount = await CountOrderItemsAsync(orderId);
        Assert.Equal(2, itemCount);
    }

    [Fact]
    public async Task UpdateSyncedAsync__WithoutItems__ShouldUpdateOrderWithNoItems()
    {
        // Arrange
        await _repository.UpsertFromWebhookAsync(_tenantId, "shopee", "SYNC-02", "COMPLETED");
        var pendingOrders = await _repository.GetPendingCompletedOrdersAsync(_tenantId);
        var orderId = pendingOrders[0].OrderId;

        var detail = MakeOrderDetail("SYNC-02");

        // Act: items list ว่าง — branch `items.Count > 0` ไม่ควรเรียก delete/insert
        await _repository.UpdateSyncedAsync(orderId, detail, 100m, null, new List<OrderItem>());

        // Assert: order ยัง synced
        var row = await GetOrderRowAsync("SYNC-02");
        Assert.Equal("synced", row!.Value.SyncStatus);
        Assert.Null(row!.Value.NetMargin);

        // ไม่มี items ถูก insert
        var itemCount = await CountOrderItemsAsync(orderId);
        Assert.Equal(0, itemCount);
    }

    [Fact]
    public async Task UpdateSyncedAsync__CalledTwice__ShouldReplaceItems()
    {
        // Arrange
        await _repository.UpsertFromWebhookAsync(_tenantId, "shopee", "SYNC-03", "COMPLETED");
        var pending = await _repository.GetPendingCompletedOrdersAsync(_tenantId);
        var orderId = pending[0].OrderId;
        var detail = MakeOrderDetail("SYNC-03");

        // First sync: 3 items
        var firstItems = Enumerable.Range(1, 3)
            .Select(i => new OrderItem(Guid.NewGuid(), orderId, _tenantId, $"SKU-{i}", null, null, 1, 100m))
            .ToList();
        await _repository.UpdateSyncedAsync(orderId, detail, 300m, null, firstItems);

        // Second sync: 1 item — delete + re-insert
        var secondItems = new List<OrderItem>
        {
            new(Guid.NewGuid(), orderId, _tenantId, "SKU-FINAL", null, null, 1, 500m)
        };
        await _repository.UpdateSyncedAsync(orderId, detail, 500m, 200m, secondItems);

        var itemCount = await CountOrderItemsAsync(orderId);
        Assert.Equal(1, itemCount);
    }

    // ─── SetSyncErrorAsync ────────────────────────────────────────────────────

    [Fact]
    public async Task SetSyncErrorAsync__ShouldUpdateSyncStatusAndError()
    {
        await _repository.UpsertFromWebhookAsync(_tenantId, "shopee", "ERR-01", "COMPLETED");
        var pending = await _repository.GetPendingCompletedOrdersAsync(_tenantId);
        var orderId = pending[0].OrderId;

        await _repository.SetSyncErrorAsync(orderId, "API timeout");

        var row = await GetOrderByIdAsync(orderId);
        Assert.Equal("error", row!.Value.SyncStatus);
        Assert.Equal("API timeout", row!.Value.SyncError);
    }

    // ─── SetAuthErrorForTenantAsync ───────────────────────────────────────────

    [Fact]
    public async Task SetAuthErrorForTenantAsync__ShouldOnlyTouchPendingRows()
    {
        // pending order
        await _repository.UpsertFromWebhookAsync(_tenantId, "shopee", "AUTH-PENDING", "COMPLETED");
        // synced order — ไม่ควรถูกแตะ
        var syncedId = await InsertSyncedOrderAsync("AUTH-SYNCED", revenue: 100m);

        await _repository.SetAuthErrorForTenantAsync(_tenantId);

        var pendingRow = await GetOrderRowAsync("AUTH-PENDING");
        var syncedRow = await GetOrderByIdAsync(syncedId);

        Assert.Equal("auth_error", pendingRow!.Value.SyncStatus);
        Assert.Equal("synced", syncedRow!.Value.SyncStatus); // ไม่เปลี่ยน
    }

    // ─── GetOrdersAsync ───────────────────────────────────────────────────────

    [Fact]
    public async Task GetOrdersAsync__WithDateRange__ShouldReturnFiltered()
    {
        var now = DateTimeOffset.UtcNow;
        await InsertOrderWithReceivedAtAsync("DATE-WITHIN", now.AddHours(-1));
        await InsertOrderWithReceivedAtAsync("DATE-OUTSIDE", now.AddDays(-2));

        var page = await _repository.GetOrdersAsync(
            _tenantId,
            from: now.AddHours(-3),
            to: now.AddHours(1),
            page: 1,
            pageSize: 50);

        Assert.Single(page.Orders);
        Assert.Equal("DATE-WITHIN", page.Orders[0].OrderSn);
        Assert.Equal(1, page.TotalCount);
    }

    [Fact]
    public async Task GetOrdersAsync__ShouldPaginate()
    {
        var now = DateTimeOffset.UtcNow;
        for (var i = 0; i < 5; i++)
            await InsertOrderWithReceivedAtAsync($"PAGE-{i:D2}", now.AddMinutes(-i));

        var page1 = await _repository.GetOrdersAsync(
            _tenantId,
            from: now.AddHours(-1),
            to: now.AddHours(1),
            page: 1,
            pageSize: 2);

        Assert.Equal(2, page1.Orders.Count);
        Assert.Equal(5, page1.TotalCount);
    }

    // ─── GetSummaryAsync ──────────────────────────────────────────────────────

    [Fact]
    public async Task GetSummaryAsync__CompletedOrders__ShouldAggregateMargins()
    {
        var now = DateTimeOffset.UtcNow;
        // synced order 1: revenue=1000, gross=200, net=100
        await InsertSyncedOrderWithReceivedAtAsync("SUM-01", now.AddHours(-1), revenue: 1000m, gross: 200m, net: 100m);
        // synced order 2: revenue=500, gross=80, net=null
        await InsertSyncedOrderWithReceivedAtAsync("SUM-02", now.AddHours(-1), revenue: 500m, gross: 80m, net: null);
        // pending order — ไม่ควรนับ
        await InsertOrderWithReceivedAtAsync("SUM-PENDING", now.AddHours(-1));

        var summary = await _repository.GetSummaryAsync(
            _tenantId,
            from: now.AddHours(-3),
            to: now.AddHours(1));

        Assert.Equal(1500m, summary.TotalRevenue);
        Assert.Equal(280m, summary.TotalGrossMargin);
        Assert.Equal(100m, summary.TotalNetMargin); // SUM-02 มี net=null ไม่นับ
        Assert.Equal(2, summary.CompletedOrderCount);
        Assert.Equal(1, summary.PendingSettlementCount);
    }

    [Fact]
    public async Task GetSummaryAsync__EmptyRange__ShouldReturnZeroes()
    {
        var farFuture = DateTimeOffset.UtcNow.AddDays(100);
        var summary = await _repository.GetSummaryAsync(
            _tenantId,
            from: farFuture,
            to: farFuture.AddHours(1));

        Assert.Equal(0m, summary.TotalRevenue);
        Assert.Equal(0m, summary.TotalGrossMargin);
        Assert.Null(summary.TotalNetMargin);
        Assert.Equal(0, summary.CompletedOrderCount);
    }

    // ─── GetSyncProgressAsync ─────────────────────────────────────────────────

    [Fact]
    public async Task GetSyncProgressAsync__ShouldReturnCounts()
    {
        await InsertSyncedOrderAsync("PROG-SYNCED-1", revenue: 100m);
        await InsertSyncedOrderAsync("PROG-SYNCED-2", revenue: 200m);
        await _repository.UpsertFromWebhookAsync(_tenantId, "shopee", "PROG-PENDING", "COMPLETED");

        var progress = await _repository.GetSyncProgressAsync(_tenantId);

        Assert.Equal(2, progress.Synced);
        Assert.Equal(1, progress.Pending);
        Assert.Equal(3, progress.Total);
    }

    // ─── Helpers ─────────────────────────────────────────────────────────────

    private static ShopeeOrderDetail MakeOrderDetail(string orderSn) => new(
        OrderSn: orderSn,
        Status: "COMPLETED",
        EscrowTotalAmount: 1500m,
        EscrowShopeeCommission: 75m,
        EscrowShipRebate: 25m,
        EscrowVoucher: 10m,
        EscrowPromotion: 5m,
        OrderCreateTime: DateTimeOffset.UtcNow.AddDays(-1),
        Items: []);

    private async Task<(string Status, string SyncStatus, decimal? Revenue, decimal? GrossMargin, decimal? NetMargin)?> GetOrderRowAsync(string orderSn)
    {
        await using var conn = new NpgsqlConnection(_fixture.ConnectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(
            "SELECT status, sync_status, revenue, gross_margin, net_margin FROM orders WHERE tenant_id = @tid AND order_sn = @sn",
            conn);
        cmd.Parameters.AddWithValue("tid", _tenantId);
        cmd.Parameters.AddWithValue("sn", orderSn);
        await using var r = await cmd.ExecuteReaderAsync();
        if (!await r.ReadAsync()) return null;
        return (
            r.GetString(0),
            r.GetString(1),
            r.IsDBNull(2) ? null : r.GetDecimal(2),
            r.IsDBNull(3) ? null : r.GetDecimal(3),
            r.IsDBNull(4) ? null : r.GetDecimal(4)
        );
    }

    private async Task<(string SyncStatus, string? SyncError)?> GetOrderByIdAsync(Guid orderId)
    {
        await using var conn = new NpgsqlConnection(_fixture.ConnectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(
            "SELECT sync_status, sync_error FROM orders WHERE id = @id",
            conn);
        cmd.Parameters.AddWithValue("id", orderId);
        await using var r = await cmd.ExecuteReaderAsync();
        if (!await r.ReadAsync()) return null;
        return (r.GetString(0), r.IsDBNull(1) ? null : r.GetString(1));
    }

    private async Task<int> CountOrderItemsAsync(Guid orderId)
    {
        await using var conn = new NpgsqlConnection(_fixture.ConnectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(
            "SELECT COUNT(*) FROM order_items WHERE order_id = @id",
            conn);
        cmd.Parameters.AddWithValue("id", orderId);
        return Convert.ToInt32(await cmd.ExecuteScalarAsync());
    }

    /// <summary>
    /// Insert order ที่ status=COMPLETED + sync_status=synced โดยตรง
    /// คืน order id เพื่อใช้ใน assertions
    /// </summary>
    private async Task<Guid> InsertSyncedOrderAsync(
        string orderSn, decimal revenue, decimal grossMargin = 0m, decimal? netMargin = null)
    {
        await using var conn = new NpgsqlConnection(_fixture.ConnectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand("""
            INSERT INTO orders
              (tenant_id, platform, order_sn, status, revenue, gross_margin, net_margin, sync_status)
            VALUES
              (@tenantId, 'shopee', @orderSn, 'COMPLETED', @revenue, @gross, @net, 'synced')
            RETURNING id
            """, conn);
        cmd.Parameters.AddWithValue("tenantId", _tenantId);
        cmd.Parameters.AddWithValue("orderSn", orderSn);
        cmd.Parameters.AddWithValue("revenue", revenue);
        cmd.Parameters.AddWithValue("gross", grossMargin);
        cmd.Parameters.AddWithValue("net", netMargin.HasValue ? (object)netMargin.Value : DBNull.Value);
        return (Guid)(await cmd.ExecuteScalarAsync())!;
    }

    private async Task InsertOrderWithReceivedAtAsync(string orderSn, DateTimeOffset receivedAt)
    {
        await using var conn = new NpgsqlConnection(_fixture.ConnectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand("""
            INSERT INTO orders (tenant_id, platform, order_sn, status, received_at)
            VALUES (@tenantId, 'shopee', @orderSn, 'READY_TO_SHIP', @receivedAt)
            """, conn);
        cmd.Parameters.AddWithValue("tenantId", _tenantId);
        cmd.Parameters.AddWithValue("orderSn", orderSn);
        cmd.Parameters.AddWithValue("receivedAt", receivedAt);
        await cmd.ExecuteNonQueryAsync();
    }

    private async Task InsertSyncedOrderWithReceivedAtAsync(
        string orderSn, DateTimeOffset receivedAt,
        decimal revenue, decimal gross, decimal? net)
    {
        await using var conn = new NpgsqlConnection(_fixture.ConnectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand("""
            INSERT INTO orders
              (tenant_id, platform, order_sn, status, revenue, gross_margin, net_margin, sync_status, received_at)
            VALUES
              (@tenantId, 'shopee', @orderSn, 'COMPLETED', @revenue, @gross, @net, 'synced', @receivedAt)
            """, conn);
        cmd.Parameters.AddWithValue("tenantId", _tenantId);
        cmd.Parameters.AddWithValue("orderSn", orderSn);
        cmd.Parameters.AddWithValue("revenue", revenue);
        cmd.Parameters.AddWithValue("gross", gross);
        cmd.Parameters.AddWithValue("net", net.HasValue ? (object)net.Value : DBNull.Value);
        cmd.Parameters.AddWithValue("receivedAt", receivedAt);
        await cmd.ExecuteNonQueryAsync();
    }
}
