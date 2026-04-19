using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Ryla.Api.Extensions;
using Ryla.Core.Configuration;
using Ryla.Core.Domain.Webhooks;
using Ryla.Core.Services;

namespace Ryla.Api.Endpoints;

internal static class ShopeeWebhookEndpoints
{
    public static IEndpointRouteBuilder MapShopeeWebhookEndpoints(
        this IEndpointRouteBuilder app)
    {
        app.MapPost("/webhooks/shopee", ReceiveWebhook)
            .AllowAnonymous() // webhook ไม่มี user auth — ใช้ HMAC signature แทน
            .WithName("ReceiveShopeeWebhook")
            .WithSummary("Shopee webhook receiver")
            .WithDescription("รับ push notification จาก Shopee Open Platform พร้อม HMAC verification")
            .WithTags("Webhooks")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status422UnprocessableEntity);

        return app;
    }

    internal static async Task<IResult> ReceiveWebhook(
        HttpContext ctx,
        IShopeeHmacVerifier verifier,
        IOptions<ShopeeOptions> options,
        ILogger<Program> logger)
    {
        // อ่าน raw body เพื่อ HMAC verification
        ctx.Request.EnableBuffering();
        using var reader = new StreamReader(ctx.Request.Body, Encoding.UTF8, leaveOpen: true);
        var rawBody = await reader.ReadToEndAsync();
        ctx.Request.Body.Position = 0;

        // Shopee ส่ง signature มาทาง query parameter ?authorization={hex}
        var authorization = ctx.Request.Query["authorization"].FirstOrDefault();
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

        // Replay protection: timestamp อยู่ใน body (ต่างจาก TikTok ที่อยู่ใน header)
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

        return Results.Ok();
    }
}
