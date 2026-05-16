namespace Ryla.Core.Domain.Orders;

/// <summary>
/// Domain model สำหรับ order ใน profit dashboard
/// Financial fields เป็น null จนกว่า status = COMPLETED และ sync เสร็จ
/// </summary>
public sealed record ShopeeOrder(
    Guid Id,
    Guid TenantId,
    string OrderSn,
    string Status,
    string SyncStatus,
    decimal? Revenue,
    decimal? Commission,
    decimal? ShippingRebate,
    decimal? VoucherDeduct,
    decimal? GrossMargin,
    decimal? NetMargin,
    DateTimeOffset? OrderCreatedAt,
    DateTimeOffset? SyncedAt,
    DateTimeOffset ReceivedAt);
