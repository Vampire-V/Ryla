using Microsoft.Extensions.Logging;
using Ryla.Core.Domain.Webhooks;
using Ryla.Core.Ports.Outbound;

namespace Ryla.Core.UseCases;

/// <summary>
/// Core use case: รับ order webhook context → หา tenant → push LINE + append Google Sheets
/// ทุก failure path คืน skip/fail result (ไม่ throw) เพราะ webhook ต้องได้ 200 เสมอ
/// Sheets failure เป็น fail-soft — ไม่กระทบ LINE delivery และ HTTP 200
/// </summary>
internal sealed class ProcessOrderWebhookUseCase(
    IConnectionRepository connectionRepository,
    ILineNotifier lineNotifier,
    ISheetAppender sheetAppender,
    ILogger<ProcessOrderWebhookUseCase> logger) : IProcessOrderWebhookUseCase
{
    public async Task<ProcessOrderResult> ExecuteAsync(
        OrderWebhookContext ctx,
        CancellationToken ct = default)
    {
        var tenantId = await connectionRepository.FindTenantIdByShopIdAsync(
            ctx.Platform, ctx.ShopId, ct);

        if (tenantId is null)
        {
            logger.LogInformation(
                "No tenant found for {Platform} shopId={ShopId} — skipping all notifications",
                ctx.Platform, ctx.ShopId);
            return new ProcessOrderResult(
                ProcessOrderStatus.SkippedNoTenant,
                SheetsStatus: SheetsDeliveryStatus.SkippedNoTenant);
        }

        var (lineStatus, lineError) = await ProcessLineAsync(tenantId.Value, ctx, ct);
        var sheetsStatus = await ProcessSheetsAsync(tenantId.Value, ctx, ct);

        return BuildResult(lineStatus, lineError, sheetsStatus);
    }

    private async Task<(LineDeliveryStatus Status, string? ErrorMessage)> ProcessLineAsync(
        Guid tenantId,
        OrderWebhookContext ctx,
        CancellationToken ct)
    {
        var lineCreds = await connectionRepository.GetLineCredentialsAsync(tenantId, ct);
        if (lineCreds is null)
        {
            logger.LogInformation(
                "No LINE config for tenant={TenantId} — skipping LINE notification", tenantId);
            return (LineDeliveryStatus.SkippedNoConfig, null);
        }

        var message = FormatLineMessage(ctx.Platform, ctx.OrderId, ctx.EventType);
        var result = await lineNotifier.PushAsync(
            lineCreds.ChannelAccessToken, lineCreds.TargetUserId, message, ct);

        if (!result.Success)
        {
            logger.LogWarning(
                "LINE push failed for tenant={TenantId} errorCode={ErrorCode}: {ErrorMessage}",
                tenantId, result.ErrorCode, result.ErrorMessage);
            return (LineDeliveryStatus.Failed, result.ErrorMessage);
        }

        logger.LogInformation(
            "LINE notification sent for tenant={TenantId} order={OrderId}", tenantId, ctx.OrderId);
        return (LineDeliveryStatus.Sent, null);
    }

    private async Task<SheetsDeliveryStatus> ProcessSheetsAsync(
        Guid tenantId,
        OrderWebhookContext ctx,
        CancellationToken ct)
    {
        var sheetsCreds = await connectionRepository.GetGoogleSheetsCredentialsAsync(tenantId, ct);
        if (sheetsCreds is null)
        {
            logger.LogInformation(
                "No Sheets config for tenant={TenantId} — skipping Sheets append", tenantId);
            return SheetsDeliveryStatus.SkippedNoConfig;
        }

        var row = FormatRow(ctx);
        var result = await sheetAppender.AppendRowAsync(sheetsCreds, row, ct);

        if (!result.Success)
        {
            logger.LogWarning(
                "Sheets append failed for tenant={TenantId} errorCode={ErrorCode}: {ErrorMessage}",
                tenantId, result.ErrorCode, result.ErrorMessage);
            return SheetsDeliveryStatus.Failed;
        }

        logger.LogInformation(
            "Sheets row appended for tenant={TenantId} order={OrderId}", tenantId, ctx.OrderId);
        return SheetsDeliveryStatus.Sent;
    }

    private static ProcessOrderResult BuildResult(
        LineDeliveryStatus lineStatus,
        string? lineError,
        SheetsDeliveryStatus sheetsStatus)
    {
        // Overall Sent if EITHER channel delivered
        var eitherSent = lineStatus == LineDeliveryStatus.Sent
                      || sheetsStatus == SheetsDeliveryStatus.Sent;

        if (eitherSent)
            return new ProcessOrderResult(ProcessOrderStatus.Sent, lineStatus, sheetsStatus);

        // LINE specific failures preserved for backward compat
        if (lineStatus == LineDeliveryStatus.Failed)
            return new ProcessOrderResult(
                ProcessOrderStatus.FailedLineDelivery, lineStatus, sheetsStatus, lineError);

        // Both skipped → SkippedNoLineConfig (no Sheets config is not an error on its own)
        return new ProcessOrderResult(
            ProcessOrderStatus.SkippedNoLineConfig, lineStatus, sheetsStatus);
    }

    private static IReadOnlyList<string> FormatRow(OrderWebhookContext ctx) =>
    [
        DateTime.UtcNow.ToString("o"),
        PlatformDisplayName(ctx.Platform),
        ctx.OrderId,
        ctx.EventType
    ];

    private static string FormatLineMessage(Platform platform, string orderId, string eventType)
        => $"\U0001f514 {PlatformDisplayName(platform)}: Order #{orderId} \u2014 {eventType}";

    private static string PlatformDisplayName(Platform platform) => platform switch
    {
        Platform.TikTokShop => "TikTok Shop",
        Platform.Shopee => "Shopee",
        _ => platform.ToString()
    };
}
