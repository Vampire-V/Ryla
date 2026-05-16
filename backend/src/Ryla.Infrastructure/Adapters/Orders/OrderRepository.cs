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
    public async Task UpsertFromWebhookAsync(
        Guid tenantId, string platform, string orderSn, string status,
        CancellationToken ct = default)
    {
        await using var conn = await connectionFactory.CreateAsync(ct);
        await using var cmd = conn.CreateCommand();

        // COALESCE: ถ้า financial fields มีค่าอยู่แล้ว (sync แล้ว) ไม่ overwrite ด้วย NULL
        cmd.CommandText = """
            INSERT INTO orders (tenant_id, platform, order_sn, status)
            VALUES (@tenantId, @platform, @orderSn, @status)
            ON CONFLICT (tenant_id, platform, order_sn) DO UPDATE SET
              status     = EXCLUDED.status,
              updated_at = now()
            """;
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

        cmd.CommandText = """
            SELECT id, order_sn
            FROM orders
            WHERE tenant_id = @tenantId
              AND status = 'COMPLETED'
              AND sync_status = 'pending'
            ORDER BY received_at
            LIMIT @limit
            """;
        cmd.Parameters.AddWithValue("tenantId", tenantId);
        cmd.Parameters.AddWithValue("limit", limit);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
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
            await using var updateCmd = conn.CreateCommand();
            updateCmd.Transaction = transaction;
            updateCmd.CommandText = """
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
            updateCmd.Parameters.AddWithValue("orderId", orderId);
            updateCmd.Parameters.AddWithValue("status", detail.Status);
            updateCmd.Parameters.AddWithValue("revenue", detail.EscrowTotalAmount);
            updateCmd.Parameters.AddWithValue("commission", detail.EscrowShopeeCommission);
            updateCmd.Parameters.AddWithValue("shippingRebate", detail.EscrowShipRebate);
            updateCmd.Parameters.AddWithValue("voucherDeduct", detail.EscrowVoucher + detail.EscrowPromotion);
            updateCmd.Parameters.AddWithValue("grossMargin", grossMargin);
            updateCmd.Parameters.AddWithValue("netMargin", netMargin.HasValue ? (object)netMargin.Value : DBNull.Value);
            updateCmd.Parameters.AddWithValue("orderCreatedAt", detail.OrderCreateTime);
            await updateCmd.ExecuteNonQueryAsync(ct);

            if (items.Count > 0)
            {
                // Delete + re-insert สะอาดกว่า partial upsert
                await using var deleteCmd = conn.CreateCommand();
                deleteCmd.Transaction = transaction;
                deleteCmd.CommandText = "DELETE FROM order_items WHERE order_id = @orderId";
                deleteCmd.Parameters.AddWithValue("orderId", orderId);
                await deleteCmd.ExecuteNonQueryAsync(ct);

                foreach (var item in items)
                {
                    await using var insertCmd = conn.CreateCommand();
                    insertCmd.Transaction = transaction;
                    insertCmd.CommandText = """
                        INSERT INTO order_items
                          (id, order_id, tenant_id, item_sku, model_sku, item_name, quantity, item_price)
                        VALUES
                          (@id, @orderId, @tenantId, @itemSku, @modelSku, @itemName, @quantity, @itemPrice)
                        """;
                    insertCmd.Parameters.AddWithValue("id", item.Id);
                    insertCmd.Parameters.AddWithValue("orderId", orderId);
                    insertCmd.Parameters.AddWithValue("tenantId", item.TenantId);
                    insertCmd.Parameters.AddWithValue("itemSku", item.ItemSku);
                    insertCmd.Parameters.AddWithValue("modelSku", item.ModelSku is null ? DBNull.Value : (object)item.ModelSku);
                    insertCmd.Parameters.AddWithValue("itemName", item.ItemName is null ? DBNull.Value : (object)item.ItemName);
                    insertCmd.Parameters.AddWithValue("quantity", item.Quantity);
                    insertCmd.Parameters.AddWithValue("itemPrice", item.ItemPrice.HasValue ? (object)item.ItemPrice.Value : DBNull.Value);
                    await insertCmd.ExecuteNonQueryAsync(ct);
                }
            }

            await transaction.CommitAsync(ct);
        }
        catch
        {
            await transaction.RollbackAsync(ct);
            throw;
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
        var offset = (page - 1) * pageSize;

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
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
        cmd.Parameters.AddWithValue("tenantId", tenantId);
        cmd.Parameters.AddWithValue("from", from);
        cmd.Parameters.AddWithValue("to", to);
        cmd.Parameters.AddWithValue("limit", pageSize);
        cmd.Parameters.AddWithValue("offset", offset);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        var orders = new List<ShopeeOrder>();
        var totalCount = 0;

        while (await reader.ReadAsync(ct))
        {
            if (orders.Count == 0)
                totalCount = reader.GetInt32(reader.GetOrdinal("total_count"));

            orders.Add(MapOrder(reader));
        }

        return new OrdersPage(orders, totalCount);
    }

    public async Task<ProfitSummary> GetSummaryAsync(
        Guid tenantId, DateTimeOffset from, DateTimeOffset to,
        CancellationToken ct = default)
    {
        await using var conn = await connectionFactory.CreateAsync(ct);
        await using var cmd = conn.CreateCommand();

        cmd.CommandText = """
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
        cmd.Parameters.AddWithValue("tenantId", tenantId);
        cmd.Parameters.AddWithValue("from", from);
        cmd.Parameters.AddWithValue("to", to);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
            return ProfitSummary.Empty();

        var revenue = reader.GetDecimal(0);
        var grossMargin = reader.GetDecimal(1);
        var netMargin = reader.IsDBNull(2) ? (decimal?)null : reader.GetDecimal(2);
        var completedCount = reader.GetInt32(3);
        var pendingCount = reader.GetInt32(4);

        var grossPct = revenue > 0 ? Math.Round(grossMargin / revenue * 100, 2) : (decimal?)null;
        var netPct = revenue > 0 && netMargin.HasValue
            ? Math.Round(netMargin.Value / revenue * 100, 2) : (decimal?)null;

        return new ProfitSummary(revenue, grossMargin, netMargin, grossPct, netPct, completedCount, pendingCount);
    }

    public async Task<SyncProgress> GetSyncProgressAsync(Guid tenantId, CancellationToken ct = default)
    {
        await using var conn = await connectionFactory.CreateAsync(ct);
        await using var cmd = conn.CreateCommand();

        cmd.CommandText = """
            SELECT
              COUNT(*) FILTER (WHERE sync_status = 'synced') AS synced,
              COUNT(*) FILTER (WHERE sync_status = 'pending') AS pending,
              COUNT(*) AS total
            FROM orders
            WHERE tenant_id = @tenantId
            """;
        cmd.Parameters.AddWithValue("tenantId", tenantId);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
            return new SyncProgress(0, 0, 0);

        return new SyncProgress(reader.GetInt32(0), reader.GetInt32(1), reader.GetInt32(2));
    }

    private static ShopeeOrder MapOrder(Npgsql.NpgsqlDataReader r) => new(
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
