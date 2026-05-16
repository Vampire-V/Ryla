namespace Ryla.Core.Domain.Orders;

/// <summary>
/// Aggregated profit metrics สำหรับ KPI cards ใน dashboard
/// นับเฉพาะ COMPLETED orders ที่ sync แล้ว
/// </summary>
public sealed record ProfitSummary(
    decimal TotalRevenue,
    decimal TotalGrossMargin,
    decimal? TotalNetMargin,
    decimal? GrossMarginPercent,
    decimal? NetMarginPercent,
    int CompletedOrderCount,
    int PendingSettlementCount)
{
    public static ProfitSummary Empty(int pendingCount = 0) =>
        new(0m, 0m, null, null, null, 0, pendingCount);
}
