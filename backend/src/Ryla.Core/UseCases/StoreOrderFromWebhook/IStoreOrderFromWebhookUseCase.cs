namespace Ryla.Core.UseCases.StoreOrderFromWebhook;

/// <summary>
/// Use case: รับ Shopee webhook → upsert pending row ใน orders table
/// แยกออกจาก ProcessOrderWebhookUseCase (LINE/Sheets) เพื่อ SRP
/// </summary>
public interface IStoreOrderFromWebhookUseCase
{
    Task ExecuteAsync(
        Guid tenantId,
        string orderSn,
        string status,
        CancellationToken ct = default);
}
