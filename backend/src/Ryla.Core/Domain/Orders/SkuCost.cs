namespace Ryla.Core.Domain.Orders;

/// <summary>
/// COGS ที่ user กรอกเอง — parent SKU level (MVP: ไม่แยก variant)
/// </summary>
public sealed record SkuCost(
    Guid Id,
    Guid TenantId,
    string Platform,
    string ItemSku,
    string? ItemName,
    decimal Cogs);
