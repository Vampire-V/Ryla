using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using Ryla.Api.Extensions;
using Ryla.Core.Configuration;
using Ryla.Core.Domain.Webhooks;
using Ryla.Core.Services;

namespace Ryla.Api.Endpoints;

internal static class TikTokWebhookEndpoints
{
    public static IEndpointRouteBuilder MapTikTokWebhookEndpoints(
        this IEndpointRouteBuilder app)
    {
        app.MapPost("/webhooks/tiktok-shop", ReceiveWebhook)
            .AllowAnonymous() // webhook ไม่มี user auth — ใช้ HMAC signature แทน
            .WithName("ReceiveTikTokShopWebhook")
            .WithSummary("TikTok Shop webhook receiver")
            .WithDescription("รับ event จาก TikTok Shop Partner API พร้อม HMAC verification")
            .WithTags("Webhooks")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status422UnprocessableEntity);

        return app;
    }

    internal static async Task<IResult> ReceiveWebhook(
        HttpContext ctx,
        ITikTokHmacVerifier verifier,
        ILogger<Program> logger)
    {
        // อ่าน raw body เพื่อ HMAC verification
        // ต้องอ่านก่อน model binding เพราะ HMAC คำนวณจาก raw bytes
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

        // Parse payload — จับ JsonException เพื่อ return 422 แทน 500
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

        return Results.Ok();
    }
}
