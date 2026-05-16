using Ryla.Core.Domain.Orders;

namespace Ryla.Core.Ports.Outbound;

/// <summary>
/// Port สำหรับ sku_costs table — COGS management
/// </summary>
public interface ISkuCostRepository
{
    /// <summary>
    /// สร้างหรืออัปเดต COGS ต่อ item_sku
    /// ON CONFLICT (tenant_id, platform, item_sku) DO UPDATE
    /// </summary>
    Task<SkuCost> UpsertAsync(
        Guid tenantId,
        string platform,
        string itemSku,
        string? itemName,
        decimal cogs,
        CancellationToken ct = default);

    /// <summary>
    /// ดึง COGS ทั้งหมดของ tenant
    /// </summary>
    Task<IReadOnlyList<SkuCost>> GetByTenantAsync(
        Guid tenantId,
        string platform,
        CancellationToken ct = default);

    /// <summary>
    /// ดึง COGS map สำหรับคำนวณ net_margin: item_sku → cost
    /// </summary>
    Task<IReadOnlyDictionary<string, decimal>> GetCostMapAsync(
        Guid tenantId,
        string platform,
        IReadOnlyList<string> itemSkus,
        CancellationToken ct = default);
}
