using Ryla.Core.Domain.Orders;
using Ryla.Core.Ports.Outbound;
using Ryla.Infrastructure.Adapters.Database;

namespace Ryla.Infrastructure.Adapters.Orders;

internal sealed class SkuCostRepository(IDbConnectionFactory connectionFactory) : ISkuCostRepository
{
    public async Task<SkuCost> UpsertAsync(
        Guid tenantId, string platform, string itemSku, string? itemName, decimal cogs,
        CancellationToken ct = default)
    {
        await using var conn = await connectionFactory.CreateAsync(ct);
        await using var cmd = conn.CreateCommand();

        cmd.CommandText = """
            INSERT INTO sku_costs (tenant_id, platform, item_sku, item_name, cogs)
            VALUES (@tenantId, @platform, @itemSku, @itemName, @cogs)
            ON CONFLICT (tenant_id, platform, item_sku) DO UPDATE SET
              item_name  = EXCLUDED.item_name,
              cogs       = EXCLUDED.cogs,
              updated_at = now()
            RETURNING id, tenant_id, platform, item_sku, item_name, cogs
            """;
        cmd.Parameters.AddWithValue("tenantId", tenantId);
        cmd.Parameters.AddWithValue("platform", platform);
        cmd.Parameters.AddWithValue("itemSku", itemSku);
        cmd.Parameters.AddWithValue("itemName", itemName is null ? DBNull.Value : (object)itemName);
        cmd.Parameters.AddWithValue("cogs", cogs);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        await reader.ReadAsync(ct);
        return MapSkuCost(reader);
    }

    public async Task<IReadOnlyList<SkuCost>> GetByTenantAsync(
        Guid tenantId, string platform, CancellationToken ct = default)
    {
        await using var conn = await connectionFactory.CreateAsync(ct);
        await using var cmd = conn.CreateCommand();

        cmd.CommandText = """
            SELECT id, tenant_id, platform, item_sku, item_name, cogs
            FROM sku_costs
            WHERE tenant_id = @tenantId AND platform = @platform
            ORDER BY item_sku
            """;
        cmd.Parameters.AddWithValue("tenantId", tenantId);
        cmd.Parameters.AddWithValue("platform", platform);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        var results = new List<SkuCost>();
        while (await reader.ReadAsync(ct))
            results.Add(MapSkuCost(reader));

        return results;
    }

    public async Task<IReadOnlyDictionary<string, decimal>> GetCostMapAsync(
        Guid tenantId, string platform, IReadOnlyList<string> itemSkus,
        CancellationToken ct = default)
    {
        if (itemSkus.Count == 0)
            return new Dictionary<string, decimal>();

        await using var conn = await connectionFactory.CreateAsync(ct);
        await using var cmd = conn.CreateCommand();

        cmd.CommandText = """
            SELECT item_sku, cogs
            FROM sku_costs
            WHERE tenant_id = @tenantId
              AND platform = @platform
              AND item_sku = ANY(@skus)
            """;
        cmd.Parameters.AddWithValue("tenantId", tenantId);
        cmd.Parameters.AddWithValue("platform", platform);
        cmd.Parameters.AddWithValue("skus", itemSkus.ToArray());

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        var map = new Dictionary<string, decimal>();
        while (await reader.ReadAsync(ct))
            map[reader.GetString(0)] = reader.GetDecimal(1);

        return map;
    }

    private static SkuCost MapSkuCost(Npgsql.NpgsqlDataReader r) => new(
        Id: r.GetGuid(0),
        TenantId: r.GetGuid(1),
        Platform: r.GetString(2),
        ItemSku: r.GetString(3),
        ItemName: r.IsDBNull(4) ? null : r.GetString(4),
        Cogs: r.GetDecimal(5));
}
