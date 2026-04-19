using Microsoft.Extensions.Logging;
using Ryla.Core.Domain.Webhooks;
using Ryla.Core.Ports.Outbound;

namespace Ryla.Core.UseCases;

/// <summary>
/// Core use case: รับ order webhook context → หา tenant → push LINE notification
/// ทุก failure path คืน skip/fail result (ไม่ throw) เพราะ webhook ต้องได้ 200 เสมอ
/// </summary>
internal sealed class ProcessOrderWebhookUseCase(
    IConnectionRepository connectionRepository,
    ILineNotifier lineNotifier,
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
                "No tenant found for {Platform} shopId={ShopId} — skipping LINE notification",
                ctx.Platform, ctx.ShopId);
            return new ProcessOrderResult(ProcessOrderStatus.SkippedNoTenant);
        }

        var lineCreds = await connectionRepository.GetLineCredentialsAsync(tenantId.Value, ct);
        if (lineCreds is null)
        {
            logger.LogInformation(
                "No LINE config for tenant={TenantId} — skipping LINE notification",
                tenantId.Value);
            return new ProcessOrderResult(ProcessOrderStatus.SkippedNoLineConfig);
        }

        var message = FormatMessage(ctx.Platform, ctx.OrderId, ctx.EventType);
        var result = await lineNotifier.PushAsync(
            lineCreds.ChannelAccessToken, lineCreds.TargetUserId, message, ct);

        if (!result.Success)
        {
            logger.LogWarning(
                "LINE push failed for tenant={TenantId} errorCode={ErrorCode}: {ErrorMessage}",
                tenantId.Value, result.ErrorCode, result.ErrorMessage);
            return new ProcessOrderResult(
                ProcessOrderStatus.FailedLineDelivery,
                result.ErrorMessage);
        }

        logger.LogInformation(
            "LINE notification sent for tenant={TenantId} order={OrderId}",
            tenantId.Value, ctx.OrderId);
        return new ProcessOrderResult(ProcessOrderStatus.Sent);
    }

    private static string FormatMessage(Platform platform, string orderId, string eventType)
        => $"\U0001f514 {PlatformDisplayName(platform)}: Order #{orderId} \u2014 {eventType}";

    private static string PlatformDisplayName(Platform platform) => platform switch
    {
        Platform.TikTokShop => "TikTok Shop",
        Platform.Shopee => "Shopee",
        _ => platform.ToString()
    };
}
