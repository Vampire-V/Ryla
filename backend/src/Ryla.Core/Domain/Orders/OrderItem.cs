namespace Ryla.Core.Domain.Orders;

/// <summary>
/// รายการสินค้าใน order — ใช้คำนวณ net margin ด้วย sku_costs
/// </summary>
public sealed record OrderItem(
    Guid Id,
    Guid OrderId,
    Guid TenantId,
    string ItemSku,
    string? ModelSku,
    string? ItemName,
    int Quantity,
    decimal? ItemPrice);
