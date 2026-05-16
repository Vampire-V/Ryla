using Npgsql;
using NpgsqlTypes;
using Ryla.Core.Domain.Orders;
using Ryla.Core.Ports.Outbound;
using Ryla.Infrastructure.Adapters.Database;

namespace Ryla.Infrastructure.Adapters.Orders;

/// <summary>
/// Npgsql implementation ของ IOrderRepository
/// Raw SQL — ไม่ใช้ ORM เพื่อ AOT compliance
/// </summary>
internal sealed class OrderRepository(IDbConnectionFactory connectionFactory) : IOrderRepository
{
    private const string WebhookUpsertSql = """
        INSERT INTO orders (tenant_id, platform, order_sn, status)
        VALUES (@tenantId, @platform, @orderSn, @status)
        ON CONFLICT (tenant_id, platform, order_sn) DO UPDATE SET
          status     = EXCLUDED.status,
          updated_at = now()
        """;

    private const string PendingOrdersSql = """
        SELECT id, order_sn
        FROM orders
        WHERE tenant_id = @tenantId
          AND status = 'COMPLETED'
          AND sync_status = 'pending'
        ORDER BY received_at
        LIMIT @limit
        """;

    private const string OrderItemsBatchSql = """
        INSERT INTO order_items
          (id, order_id, tenant_id, item_sku, model_sku, item_name, quantity, item_price)
        SELECT
          unnest(@ids::uuid[]),
          @orderId,
          @tenantId,
          unnest(@skus::text[]),
          unnest(@modelSkus::text[]),
          unnest(@names::text[]),
          unnest(@qtys::int[]),
          unnest(@prices::numeric[])
        """;

    private const string OrderFinancialUpdateSql = """
        UPDATE orders SET
          status           = @status,
          revenue          = @revenue,
          commission       = @commission,
          shipping_rebate  = @shippingRebate,
          voucher_deduct   = @voucherDeduct,
          gross_margin     = @grossMargin,
          net_margin       = @netMargin,
          sync_status      = 'synced',
          sync_error       = NULL,
          synced_at        = now(),
          order_created_at = @orderCreatedAt,
          updated_at       = now()
        WHERE id = @orderId
        """;

    private const string OrdersPageSql = """
        SELECT id, order_sn, status, sync_status,
               revenue, commission, shipping_rebate, voucher_deduct,
               gross_margin, net_margin, order_created_at, synced_at, received_at,
               COUNT(*) OVER() AS total_count
        FROM orders
        WHERE tenant_id = @tenantId
          AND received_at >= @from
          AND received_at <= @to
        ORDER BY order_created_at DESC NULLS LAST, received_at DESC
        LIMIT @limit OFFSET @offset
        """;

    private const string SyncProgressSql = """
        SELECT
          COUNT(*) FILTER (WHERE sync_status = 'synced') AS synced,
          COUNT(*) FILTER (WHERE sync_status = 'pending') AS pending,
          COUNT(*) AS total
        FROM orders
        WHERE tenant_id = @tenantId
        """;

    private const string SummarySql = """
        SELECT
          COALESCE(SUM(revenue) FILTER (WHERE status = 'COMPLETED' AND sync_status = 'synced'), 0) AS total_revenue,
          COALESCE(SUM(gross_margin) FILTER (WHERE status = 'COMPLETED' AND sync_status = 'synced'), 0) AS total_gross,
          SUM(net_margin) FILTER (WHERE status = 'COMPLETED' AND sync_status = 'synced' AND net_margin IS NOT NULL) AS total_net,
          COUNT(*) FILTER (WHERE status = 'COMPLETED' AND sync_status = 'synced') AS completed_count,
          COUNT(*) FILTER (WHERE status != 'COMPLETED' OR sync_status = 'pending') AS pending_count
        FROM orders
        WHERE tenant_id = @tenantId
          AND received_at >= @from
          AND received_at <= @to
        """;

    public async Task UpsertFromWebhookAsync(
        Guid tenantId, string platform, string orderSn, string status,
        CancellationToken ct = default)
    {
        await using var conn = await connectionFactory.CreateAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = WebhookUpsertSql;
        cmd.Parameters.AddWithValue("tenantId", tenantId);
        cmd.Parameters.AddWithValue("platform", platform);
        cmd.Parameters.AddWithValue("orderSn", orderSn);
        cmd.Parameters.AddWithValue("status", status);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<IReadOnlyList<PendingSyncOrder>> GetPendingCompletedOrdersAsync(
        Guid tenantId, int limit = 100, CancellationToken ct = default)
    {
        await using var conn = await connectionFactory.CreateAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = PendingOrdersSql;
        cmd.Parameters.AddWithValue("tenantId", tenantId);
        cmd.Parameters.AddWithValue("limit", limit);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        return await ReadPendingOrdersAsync(reader, ct);
    }

    private static async Task<IReadOnlyList<PendingSyncOrder>> ReadPendingOrdersAsync(
        NpgsqlDataReader reader, CancellationToken ct)
    {
        var results = new List<PendingSyncOrder>();
        while (await reader.ReadAsync(ct))
            results.Add(new PendingSyncOrder(reader.GetGuid(0), reader.GetString(1)));
        return results;
    }

    public async Task UpdateSyncedAsync(
        Guid orderId, ShopeeOrderDetail detail, decimal grossMargin, decimal? netMargin,
        IReadOnlyList<OrderItem> items, CancellationToken ct = default)
    {
        await using var conn = await connectionFactory.CreateAsync(ct);
        await using var transaction = await conn.BeginTransactionAsync(ct);
        try
        {
            await ExecuteUpdateSyncedAsync(conn, transaction, orderId, detail, grossMargin, netMargin, items, ct);
            await transaction.CommitAsync(ct);
        }
        catch
        {
            await transaction.RollbackAsync(ct);
            throw;
        }
    }

    private static async Task ExecuteUpdateSyncedAsync(
        NpgsqlConnection conn, NpgsqlTransaction transaction,
        Guid orderId, ShopeeOrderDetail detail, decimal grossMargin, decimal? netMargin,
        IReadOnlyList<OrderItem> items, CancellationToken ct)
    {
        await UpdateOrderFinancialsAsync(conn, transaction, orderId, detail, grossMargin, netMargin, ct);
        if (items.Count > 0)
        {
            await DeleteOrderItemsAsync(conn, transaction, orderId, ct);
            await InsertOrderItemsBatchAsync(conn, transaction, orderId, items, ct);
        }
    }

    public async Task SetSyncErrorAsync(Guid orderId, string error, CancellationToken ct = default)
    {
        await using var conn = await connectionFactory.CreateAsync(ct);
        await using var cmd = conn.CreateCommand();

        cmd.CommandText = """
            UPDATE orders SET
              sync_status = 'error',
              sync_error  = @error,
              updated_at  = now()
            WHERE id = @orderId
            """;
        cmd.Parameters.AddWithValue("orderId", orderId);
        cmd.Parameters.AddWithValue("error", error);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task SetAuthErrorForTenantAsync(Guid tenantId, CancellationToken ct = default)
    {
        await using var conn = await connectionFactory.CreateAsync(ct);
        await using var cmd = conn.CreateCommand();

        cmd.CommandText = """
            UPDATE orders SET
              sync_status = 'auth_error',
              updated_at  = now()
            WHERE tenant_id = @tenantId
              AND sync_status = 'pending'
            """;
        cmd.Parameters.AddWithValue("tenantId", tenantId);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<OrdersPage> GetOrdersAsync(
        Guid tenantId, DateTimeOffset from, DateTimeOffset to, int page, int pageSize,
        CancellationToken ct = default)
    {
        await using var conn = await connectionFactory.CreateAsync(ct);
        await using var cmd = BuildOrdersPageCommand(conn, tenantId, from, to, page, pageSize);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        var (orders, totalCount) = await ReadOrdersPageAsync(reader, ct);

        return new OrdersPage(orders, totalCount);
    }

    public async Task<ProfitSummary> GetSummaryAsync(
        Guid tenantId, DateTimeOffset from, DateTimeOffset to,
        CancellationToken ct = default)
    {
        await using var conn = await connectionFactory.CreateAsync(ct);
        await using var cmd = BuildSummaryCommand(conn, tenantId, from, to);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
            return ProfitSummary.Empty();

        return MapProfitSummary(reader);
    }

    public async Task<SyncProgress> GetSyncProgressAsync(Guid tenantId, CancellationToken ct = default)
    {
        await using var conn = await connectionFactory.CreateAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = SyncProgressSql;
        cmd.Parameters.AddWithValue("tenantId", tenantId);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
            return new SyncProgress(0, 0, 0);

        return new SyncProgress(reader.GetInt32(0), reader.GetInt32(1), reader.GetInt32(2));
    }

    // ─── Private Helpers ───────────────────────────────────────────────────────

    private static async Task UpdateOrderFinancialsAsync(
        NpgsqlConnection conn, NpgsqlTransaction transaction,
        Guid orderId, ShopeeOrderDetail detail,
        decimal grossMargin, decimal? netMargin, CancellationToken ct)
    {
        await using var cmd = conn.CreateCommand();
        cmd.Transaction = transaction;
        cmd.CommandText = OrderFinancialUpdateSql;
        AddOrderFinancialParams(cmd, orderId, detail, grossMargin, netMargin);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    private static void AddOrderFinancialParams(
        NpgsqlCommand cmd, Guid orderId, ShopeeOrderDetail detail,
        decimal grossMargin, decimal? netMargin)
    {
        cmd.Parameters.AddWithValue("orderId", orderId);
        cmd.Parameters.AddWithValue("status", detail.Status);
        cmd.Parameters.AddWithValue("revenue", detail.EscrowTotalAmount);
        cmd.Parameters.AddWithValue("commission", detail.EscrowShopeeCommission);
        cmd.Parameters.AddWithValue("shippingRebate", detail.EscrowShipRebate);
        cmd.Parameters.AddWithValue("voucherDeduct", detail.EscrowVoucher + detail.EscrowPromotion);
        cmd.Parameters.AddWithValue("grossMargin", grossMargin);
        cmd.Parameters.AddWithValue("netMargin", netMargin.HasValue ? (object)netMargin.Value : DBNull.Value);
        cmd.Parameters.AddWithValue("orderCreatedAt", detail.OrderCreateTime);
    }

    private static async Task DeleteOrderItemsAsync(
        NpgsqlConnection conn, NpgsqlTransaction transaction, Guid orderId, CancellationToken ct)
    {
        await using var cmd = conn.CreateCommand();
        cmd.Transaction = transaction;
        cmd.CommandText = "DELETE FROM order_items WHERE order_id = @orderId";
        cmd.Parameters.AddWithValue("orderId", orderId);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    /// <summary>
    /// Batch INSERT ด้วย UNNEST — หนึ่ง round-trip แทน N round-trips
    /// tenant_id เป็น scalar เพราะ items ทุกตัวอยู่ใน order เดียว = tenant เดียว
    /// </summary>
    private static async Task InsertOrderItemsBatchAsync(
        NpgsqlConnection conn, NpgsqlTransaction transaction,
        Guid orderId, IReadOnlyList<OrderItem> items, CancellationToken ct)
    {
        await using var cmd = conn.CreateCommand();
        cmd.Transaction = transaction;
        cmd.CommandText = OrderItemsBatchSql;
        cmd.Parameters.AddWithValue("orderId", orderId);
        cmd.Parameters.AddWithValue("tenantId", items[0].TenantId);
        AddBatchItemParams(cmd, items);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    /// <summary>
    /// เพิ่ม array parameters สำหรับ UNNEST batch INSERT
    /// แยกเป็น non-nullable (strongly-typed) และ nullable (object[] + DBNull) params
    /// </summary>
    private static void AddBatchItemParams(NpgsqlCommand cmd, IReadOnlyList<OrderItem> items)
    {
        AddNonNullableItemArrayParams(cmd, items);
        AddNullableItemArrayParams(cmd, items);
    }

    private static void AddNonNullableItemArrayParams(NpgsqlCommand cmd, IReadOnlyList<OrderItem> items)
    {
        cmd.Parameters.Add(new NpgsqlParameter<Guid[]>("ids",
            [.. items.Select(i => i.Id)]));

        cmd.Parameters.Add(new NpgsqlParameter<string[]>("skus",
            [.. items.Select(i => i.ItemSku)]));

        cmd.Parameters.Add(new NpgsqlParameter<int[]>("qtys",
            [.. items.Select(i => i.Quantity)]));
    }

    private static void AddNullableItemArrayParams(NpgsqlCommand cmd, IReadOnlyList<OrderItem> items)
    {
        cmd.Parameters.Add(new NpgsqlParameter("modelSkus", NpgsqlDbType.Array | NpgsqlDbType.Text)
        {
            Value = items.Select(i => i.ModelSku ?? (object)DBNull.Value).ToArray()
        });

        cmd.Parameters.Add(new NpgsqlParameter("names", NpgsqlDbType.Array | NpgsqlDbType.Text)
        {
            Value = items.Select(i => i.ItemName ?? (object)DBNull.Value).ToArray()
        });

        cmd.Parameters.Add(new NpgsqlParameter("prices", NpgsqlDbType.Array | NpgsqlDbType.Numeric)
        {
            Value = items.Select(i => i.ItemPrice.HasValue ? (object)i.ItemPrice.Value : DBNull.Value).ToArray()
        });
    }

    private static NpgsqlCommand BuildOrdersPageCommand(
        NpgsqlConnection conn, Guid tenantId, DateTimeOffset from, DateTimeOffset to,
        int page, int pageSize)
    {
        var cmd = conn.CreateCommand();
        cmd.CommandText = OrdersPageSql;
        cmd.Parameters.AddWithValue("tenantId", tenantId);
        cmd.Parameters.AddWithValue("from", from);
        cmd.Parameters.AddWithValue("to", to);
        cmd.Parameters.AddWithValue("limit", pageSize);
        cmd.Parameters.AddWithValue("offset", (page - 1) * pageSize);
        return cmd;
    }

    private static async Task<(List<ShopeeOrder> orders, int totalCount)> ReadOrdersPageAsync(
        NpgsqlDataReader reader, CancellationToken ct)
    {
        var orders = new List<ShopeeOrder>();
        var totalCount = 0;
        while (await reader.ReadAsync(ct))
        {
            if (orders.Count == 0)
                totalCount = reader.GetInt32(reader.GetOrdinal("total_count"));
            orders.Add(MapOrder(reader));
        }
        return (orders, totalCount);
    }

    private static NpgsqlCommand BuildSummaryCommand(
        NpgsqlConnection conn, Guid tenantId, DateTimeOffset from, DateTimeOffset to)
    {
        var cmd = conn.CreateCommand();
        cmd.CommandText = SummarySql;
        cmd.Parameters.AddWithValue("tenantId", tenantId);
        cmd.Parameters.AddWithValue("from", from);
        cmd.Parameters.AddWithValue("to", to);
        return cmd;
    }

    private static ProfitSummary MapProfitSummary(NpgsqlDataReader r)
    {
        var revenue = r.GetDecimal(0);
        var grossMargin = r.GetDecimal(1);
        var netMargin = r.IsDBNull(2) ? (decimal?)null : r.GetDecimal(2);
        var completedCount = r.GetInt32(3);
        var pendingCount = r.GetInt32(4);

        var grossPct = revenue > 0 ? Math.Round(grossMargin / revenue * 100, 2) : (decimal?)null;
        var netPct = revenue > 0 && netMargin.HasValue
            ? Math.Round(netMargin.Value / revenue * 100, 2) : (decimal?)null;

        return new ProfitSummary(revenue, grossMargin, netMargin, grossPct, netPct, completedCount, pendingCount);
    }

    private static ShopeeOrder MapOrder(NpgsqlDataReader r) => new(
        Id: r.GetGuid(r.GetOrdinal("id")),
        TenantId: Guid.Empty, // ไม่ select เพื่อประหยัด bandwidth
        OrderSn: r.GetString(r.GetOrdinal("order_sn")),
        Status: r.GetString(r.GetOrdinal("status")),
        SyncStatus: r.GetString(r.GetOrdinal("sync_status")),
        Revenue: r.IsDBNull(r.GetOrdinal("revenue")) ? null : r.GetDecimal(r.GetOrdinal("revenue")),
        Commission: r.IsDBNull(r.GetOrdinal("commission")) ? null : r.GetDecimal(r.GetOrdinal("commission")),
        ShippingRebate: r.IsDBNull(r.GetOrdinal("shipping_rebate")) ? null : r.GetDecimal(r.GetOrdinal("shipping_rebate")),
        VoucherDeduct: r.IsDBNull(r.GetOrdinal("voucher_deduct")) ? null : r.GetDecimal(r.GetOrdinal("voucher_deduct")),
        GrossMargin: r.IsDBNull(r.GetOrdinal("gross_margin")) ? null : r.GetDecimal(r.GetOrdinal("gross_margin")),
        NetMargin: r.IsDBNull(r.GetOrdinal("net_margin")) ? null : r.GetDecimal(r.GetOrdinal("net_margin")),
        OrderCreatedAt: r.IsDBNull(r.GetOrdinal("order_created_at")) ? null : r.GetFieldValue<DateTimeOffset>(r.GetOrdinal("order_created_at")),
        SyncedAt: r.IsDBNull(r.GetOrdinal("synced_at")) ? null : r.GetFieldValue<DateTimeOffset>(r.GetOrdinal("synced_at")),
        ReceivedAt: r.GetFieldValue<DateTimeOffset>(r.GetOrdinal("received_at")));
}
