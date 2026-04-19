using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Ryla.Api.Extensions;
using Ryla.Core.Configuration;
using Ryla.Core.Domain.Webhooks;
using Ryla.Core.Services;
using Ryla.Core.UseCases;

namespace Ryla.Api.Endpoints;

internal static class ShopeeWebhookEndpoints
{
    public static IEndpointRouteBuilder MapShopeeWebhookEndpoints(
        this IEndpointRouteBuilder app)
    {
        app.MapPost("/webhooks/shopee", ReceiveWebhook)
            .AllowAnonymous() // Public — HMAC signature verified in handler
            .WithName("ReceiveShopeeWebhook")
            .WithSummary("Shopee webhook receiver")
            .WithDescription("รับ push notification จาก Shopee Open Platform พร้อม HMAC verification")
            .WithTags("Webhooks")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status422UnprocessableEntity)
            .WithMetadata(new RequestSizeLimitAttribute(1_048_576)); // 1MB

        return app;
    }

    internal static async Task<IResult> ReceiveWebhook(
        HttpContext ctx,
        IShopeeHmacVerifier verifier,
        IOptions<ShopeeOptions> options,
        IProcessOrderWebhookUseCase orderUseCase,
        ILogger<Program> logger)
    {
        // อ่าน raw body เพื่อ HMAC verification
        ctx.Request.EnableBuffering();
        using var reader = new StreamReader(ctx.Request.Body, Encoding.UTF8, leaveOpen: true);
        var rawBody = await reader.ReadToEndAsync();
        ctx.Request.Body.Position = 0;

        // Shopee ส่ง signature มาทาง query parameter ?authorization={hex}
        var authorization = ctx.Request.Query[ShopeeOptions.SignatureQueryParam].FirstOrDefault();
        if (string.IsNullOrEmpty(authorization) || !verifier.Verify(rawBody, authorization))
        {
            logger.LogWarning("Shopee webhook: invalid or missing signature from {RemoteIp}",
                ctx.Connection.RemoteIpAddress);
            return Results.Unauthorized();
        }

        // Parse payload
        ShopeeWebhookPayload? payload;
        try
        {
            payload = JsonSerializer.Deserialize(rawBody, RylaJsonContext.Default.ShopeeWebhookPayload);
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "Shopee webhook: invalid JSON payload");
            return Results.UnprocessableEntity();
        }

        if (payload is null)
        {
            logger.LogWarning("Shopee webhook: failed to parse payload");
            return Results.UnprocessableEntity();
        }

        // Replay protection: timestamp อยู่ใน body
        var nowUnixSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        if (Math.Abs(nowUnixSeconds - payload.Timestamp) > options.Value.MaxAgeSeconds)
        {
            logger.LogWarning(
                "Shopee webhook: stale timestamp {Timestamp} (now={Now}, maxAge={MaxAge}s)",
                payload.Timestamp, nowUnixSeconds, options.Value.MaxAgeSeconds);
            return Results.Unauthorized();
        }

        logger.LogInformation(
            "Shopee webhook received: code={Code} shopId={ShopId} timestamp={Timestamp}",
            payload.Code, payload.ShopId, payload.Timestamp);

        // Process order event — ไม่ fail webhook ถ้า use case error
        await ProcessOrderEventAsync(payload, orderUseCase, logger, ctx.RequestAborted);

        return Results.Ok();
    }

    private static async Task ProcessOrderEventAsync(
        ShopeeWebhookPayload payload,
        IProcessOrderWebhookUseCase orderUseCase,
        ILogger<Program> logger,
        CancellationToken ct)
    {
        var shopId = payload.ShopId.ToString();
        var orderId = ExtractShopeeOrderId(payload, logger);

        var webhookCtx = new OrderWebhookContext(
            Platform: Platform.Shopee,
            ShopId: shopId,
            OrderId: orderId,
            EventType: payload.Code.ToString());

        try
        {
            var result = await orderUseCase.ExecuteAsync(webhookCtx, ct);
            logger.LogInformation(
                "Shopee order processed: status={Status} detail={Detail}",
                result.Status, result.Detail);
        }
        catch (Exception ex)
        {
            // Swallow — webhook ต้องคืน 200 เสมอ (กัน platform retry storm)
            logger.LogError(ex, "Shopee order processing failed: shopId={ShopId} orderId={OrderId}",
                shopId, orderId);
        }
    }

    private static string ExtractShopeeOrderId(ShopeeWebhookPayload payload, ILogger<Program> logger)
    {
        try
        {
            if (payload.Data.ValueKind == System.Text.Json.JsonValueKind.Object
                && payload.Data.TryGetProperty("ordersn", out var ordersnElem))
            {
                var ordersn = ordersnElem.GetString();
                return string.IsNullOrEmpty(ordersn) ? "unknown" : ordersn;
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Shopee webhook: failed to extract ordersn from data");
        }

        return "unknown";
    }
}
