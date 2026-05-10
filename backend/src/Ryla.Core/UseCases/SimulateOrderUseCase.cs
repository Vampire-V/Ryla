using Microsoft.Extensions.Logging;
using Ryla.Core.Domain.Integrations;
using Ryla.Core.Domain.Webhooks;
using Ryla.Core.Ports.Outbound;

namespace Ryla.Core.UseCases;

/// <summary>
/// สร้าง synthetic order และส่งผ่าน full pipeline (LINE + Sheets) โดยตรงจาก tenantId
/// ไม่ต้องการ ShopId — ข้าม FindTenantIdByShopIdAsync ซึ่งต้องการ real shop registration
/// Sheets failure เป็น fail-soft เหมือน ProcessOrderWebhookUseCase
/// </summary>
internal sealed class SimulateOrderUseCase(
    IConnectionRepository connectionRepository,
    ILineNotifier lineNotifier,
    ISheetAppender sheetAppender,
    ILogger<SimulateOrderUseCase> logger) : ISimulateOrderUseCase
{
    private const string EventType = "ORDER_STATUS_CHANGE";

    public async Task<SimulateOrderResult> ExecuteAsync(
        Guid tenantId,
        Platform platform,
        CancellationToken ct = default)
    {
        var orderId = $"SIM-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}";

        var (lineSuccess, lineError) = await SendLineAsync(tenantId, platform, orderId, ct);
        var (sheetsSuccess, sheetsError) = await AppendSheetsAsync(tenantId, platform, orderId, ct);

        return new SimulateOrderResult(lineSuccess, lineError, sheetsSuccess, sheetsError, orderId);
    }

    private async Task<(bool Success, string? Error)> SendLineAsync(
        Guid tenantId, Platform platform, string orderId, CancellationToken ct)
    {
        var creds = await connectionRepository.GetLineCredentialsAsync(tenantId, ct);
        if (creds is null)
        {
            logger.LogInformation("SimulateOrder: no LINE config for tenant {TenantId}", tenantId);
            return (false, "ยังไม่ได้ตั้งค่า LINE OA");
        }

        var message = $"\U0001f514 {platform.DisplayName()}: Order #{orderId} — {EventType}";
        var result = await lineNotifier.PushAsync(creds.ChannelAccessToken, creds.TargetUserId, message, ct);

        if (!result.Success)
        {
            logger.LogWarning(
                "SimulateOrder: LINE push failed for tenant {TenantId}: {Error}", tenantId, result.ErrorMessage);
            return (false, result.ErrorMessage);
        }

        logger.LogInformation("SimulateOrder: LINE sent for tenant {TenantId} order {OrderId}", tenantId, orderId);
        return (true, null);
    }

    private async Task<(bool Success, string? Error)> AppendSheetsAsync(
        Guid tenantId, Platform platform, string orderId, CancellationToken ct)
    {
        var creds = await connectionRepository.GetGoogleSheetsCredentialsAsync(tenantId, ct);
        if (creds is null)
        {
            logger.LogInformation("SimulateOrder: no Sheets config for tenant {TenantId} — skipping", tenantId);
            return (false, "ยังไม่ได้ตั้งค่า Google Sheets");
        }

        IReadOnlyList<string> row =
        [
            DateTime.UtcNow.ToString("o"),
            platform.DisplayName(),
            orderId,
            EventType,
        ];

        var result = await sheetAppender.AppendRowAsync(creds, row, ct);

        if (!result.Success)
        {
            logger.LogWarning(
                "SimulateOrder: Sheets append failed for tenant {TenantId}: {Error}", tenantId, result.ErrorMessage);
            return (false, result.ErrorMessage);
        }

        logger.LogInformation("SimulateOrder: Sheets row appended for tenant {TenantId}", tenantId);
        return (true, null);
    }
}
