namespace Ryla.Core.Domain.Orders;

/// <summary>
/// ข้อมูล financial จาก Shopee get_order_detail API
/// เก็บเฉพาะ fields ที่ใช้คำนวณ margin
/// </summary>
public sealed record ShopeeOrderDetail(
    string OrderSn,
    string Status,
    decimal EscrowTotalAmount,
    decimal EscrowShopeeCommission,
    decimal EscrowShipRebate,
    decimal EscrowVoucher,
    decimal EscrowPromotion,
    DateTimeOffset OrderCreateTime,
    IReadOnlyList<ShopeeOrderItemDetail> Items);

/// <summary>
/// รายการสินค้าจาก Shopee API response
/// </summary>
public sealed record ShopeeOrderItemDetail(
    string ItemSku,
    string? ModelSku,
    string ItemName,
    int Quantity,
    decimal ItemPrice);
