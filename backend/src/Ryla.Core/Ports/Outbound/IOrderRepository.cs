using Ryla.Core.Domain.Orders;

namespace Ryla.Core.Ports.Outbound;

/// <summary>
/// Port สำหรับ orders + order_items table
/// </summary>
public interface IOrderRepository
{
    /// <summary>
    /// สร้างหรืออัปเดต pending row จาก webhook
    /// COALESCE: ไม่ overwrite financial data ที่ sync แล้ว
    /// </summary>
    Task UpsertFromWebhookAsync(
        Guid tenantId,
        string platform,
        string orderSn,
        string status,
        CancellationToken ct = default);

    /// <summary>
    /// ดึง orders ที่ status = COMPLETED + sync_status = pending สำหรับ Azure Function
    /// </summary>
    Task<IReadOnlyList<PendingSyncOrder>> GetPendingCompletedOrdersAsync(
        Guid tenantId,
        int limit = 100,
        CancellationToken ct = default);

    /// <summary>
    /// อัปเดต financial fields หลัง get_order_detail สำเร็จ
    /// รวมการ upsert order_items ด้วย
    /// </summary>
    Task UpdateSyncedAsync(
        Guid orderId,
        ShopeeOrderDetail detail,
        decimal grossMargin,
        decimal? netMargin,
        IReadOnlyList<OrderItem> items,
        CancellationToken ct = default);

    /// <summary>
    /// Mark order ว่า sync error
    /// </summary>
    Task SetSyncErrorAsync(
        Guid orderId,
        string error,
        CancellationToken ct = default);

    /// <summary>
    /// Mark ทุก pending order ของ tenant ว่า auth_error เมื่อ refresh_token หมด
    /// </summary>
    Task SetAuthErrorForTenantAsync(
        Guid tenantId,
        CancellationToken ct = default);

    /// <summary>
    /// ดึง orders list สำหรับ orders table ใน dashboard
    /// </summary>
    Task<OrdersPage> GetOrdersAsync(
        Guid tenantId,
        DateTimeOffset from,
        DateTimeOffset to,
        int page,
        int pageSize,
        CancellationToken ct = default);

    /// <summary>
    /// ดึง aggregated summary สำหรับ KPI cards
    /// </summary>
    Task<ProfitSummary> GetSummaryAsync(
        Guid tenantId,
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken ct = default);

    /// <summary>
    /// ดึง sync progress สำหรับ onboarding wizard polling
    /// </summary>
    Task<SyncProgress> GetSyncProgressAsync(
        Guid tenantId,
        CancellationToken ct = default);
}

/// <summary>
/// Order ที่รอ sync — ข้อมูลสำหรับ Azure Function
/// </summary>
public sealed record PendingSyncOrder(Guid OrderId, string OrderSn);

/// <summary>
/// Paginated result สำหรับ orders table
/// </summary>
public sealed record OrdersPage(IReadOnlyList<ShopeeOrder> Orders, int TotalCount);

/// <summary>
/// Sync progress สำหรับ onboarding wizard progress bar
/// </summary>
public sealed record SyncProgress(int Synced, int Pending, int Total);
