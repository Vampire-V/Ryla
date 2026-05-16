using Ryla.Core.Domain.Orders;

namespace Ryla.Core.Ports.Outbound;

/// <summary>
/// Port สำหรับ Shopee get_order_detail API
/// </summary>
public interface IShopeeOrderDetailPort
{
    /// <summary>
    /// เรียก Shopee get_order_detail → ดึง financial fields
    /// คืน null ถ้า order ไม่พบหรือ API error
    /// </summary>
    Task<ShopeeOrderDetail?> GetOrderDetailAsync(
        long shopId,
        string accessToken,
        string orderSn,
        CancellationToken ct = default);
}
