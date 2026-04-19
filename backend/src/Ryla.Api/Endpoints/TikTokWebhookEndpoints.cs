using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using Ryla.Api.Extensions;
using Ryla.Core.Configuration;
using Ryla.Core.Domain.Webhooks;
using Ryla.Core.Services;
using Ryla.Core.UseCases;

namespace Ryla.Api.Endpoints;

internal static class TikTokWebhookEndpoints
{
    public static IEndpointRouteBuilder MapTikTokWebhookEndpoints(
        this IEndpointRouteBuilder app)
    {
        app.MapPost("/webhooks/tiktok-shop", ReceiveWebhook)
            .AllowAnonymous() // Public — HMAC signature verified in handler
            .WithName("ReceiveTikTokShopWebhook")
            .WithSummary("TikTok Shop webhook receiver")
            .WithDescription("รับ event จาก TikTok Shop Partner API พร้อม HMAC verification")
            .WithTags("Webhooks")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status422UnprocessableEntity)
            .WithMetadata(new RequestSizeLimitAttribute(1_048_576)); // 1MB

        return app;
    }

    internal static async Task<IResult> ReceiveWebhook(
        HttpContext ctx,
        ITikTokHmacVerifier verifier,
        IProcessOrderWebhookUseCase orderUseCase,
        ILogger<Program> logger)
    {
        // อ่าน raw body เพื่อ HMAC verification
        ctx.Request.EnableBuffering();
        using var reader = new StreamReader(ctx.Request.Body, Encoding.UTF8, leaveOpen: true);
        var rawBody = await reader.ReadToEndAsync();
        ctx.Request.Body.Position = 0;

        // ตรวจสอบ signature
        var signatureHeader = ctx.Request.Headers[TikTokShopOptions.SignatureHeaderName].FirstOrDefault();
        if (string.IsNullOrEmpty(signatureHeader) || !verifier.Verify(rawBody, signatureHeader))
        {
            logger.LogWarning("TikTok Shop webhook: invalid or missing signature from {RemoteIp}",
                ctx.Connection.RemoteIpAddress);
            return Results.Unauthorized();
        }

        // Parse payload
        TikTokWebhookPayload? payload;
        try
        {
            payload = JsonSerializer.Deserialize(rawBody, RylaJsonContext.Default.TikTokWebhookPayload);
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "TikTok Shop webhook: invalid JSON payload");
            return Results.UnprocessableEntity();
        }

        if (payload is null)
        {
            logger.LogWarning("TikTok Shop webhook: failed to parse payload");
            return Results.UnprocessableEntity();
        }

        logger.LogInformation(
            "TikTok Shop webhook received: event={Event} clientKey={ClientKey} createTime={CreateTime}",
            payload.Event, payload.ClientKey, payload.CreateTime);

        // Process order event — ไม่ fail webhook ถ้า use case error
        await ProcessOrderEventAsync(payload, orderUseCase, logger, ctx.RequestAborted);

        return Results.Ok();
    }

    private static async Task ProcessOrderEventAsync(
        TikTokWebhookPayload payload,
        IProcessOrderWebhookUseCase orderUseCase,
        ILogger<Program> logger,
        CancellationToken ct)
    {
        var (shopId, orderId) = ExtractOrderFields(payload, logger);

        var webhookCtx = new OrderWebhookContext(
            Platform: Platform.TikTokShop,
            ShopId: shopId,
            OrderId: orderId,
            EventType: payload.Event);

        var result = await orderUseCase.ExecuteAsync(webhookCtx, ct);
        logger.LogInformation(
            "TikTok order processed: status={Status} detail={Detail}",
            result.Status, result.Detail);
    }

    private static (string ShopId, string OrderId) ExtractOrderFields(
        TikTokWebhookPayload payload,
        ILogger<Program> logger)
    {
        if (string.IsNullOrEmpty(payload.Content))
            return ("unknown", "unknown");

        try
        {
            var content = JsonSerializer.Deserialize(
                payload.Content, RylaJsonContext.Default.TikTokOrderContent);
            var shopId = string.IsNullOrEmpty(content?.ShopId) ? "unknown" : content.ShopId;
            var orderId = string.IsNullOrEmpty(content?.OrderId) ? "unknown" : content.OrderId;
            return (shopId, orderId);
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "TikTok webhook: failed to parse content field");
            return ("unknown", "unknown");
        }
    }
}
