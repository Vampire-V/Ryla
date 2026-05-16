using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;
using Ryla.Core.Configuration;
using Ryla.Core.UseCases.ShopeeOAuth;

namespace Ryla.Api.Endpoints;

public sealed record AuthorizeResponse(
    [property: JsonPropertyName("authorizationUrl")] string AuthorizationUrl);

public static class ShopeeOAuthEndpoints
{
    public static IEndpointRouteBuilder MapShopeeOAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/shopee/oauth").WithTags("Shopee OAuth");

        group.MapGet("/authorize", AuthorizeAsync)
            .AllowAnonymous() // Frontend redirect — user must be logged in via frontend but Shopee doesn't send auth header
            .WithName("ShopeeOAuthAuthorize")
            .WithSummary("สร้าง Shopee authorization URL แล้ว redirect ไปยัง Shopee consent page")
            .Produces(StatusCodes.Status302Found)
            .Produces(StatusCodes.Status400BadRequest);

        group.MapGet("/callback", CallbackAsync)
            .AllowAnonymous() // Shopee redirect — ไม่มี auth header
            .WithName("ShopeeOAuthCallback")
            .WithSummary("Shopee OAuth callback — แลก code เป็น token แล้ว redirect กลับ frontend")
            .Produces(StatusCodes.Status302Found)
            .Produces(StatusCodes.Status400BadRequest);

        return app;
    }

    /// <summary>
    /// Redirect ไปยัง Shopee consent page
    /// GET /api/shopee/oauth/authorize?tenant_id={uuid}
    ///
    /// CSRF protection: embed HMAC-signed (tenant_id:timestamp) ใน redirect URL
    /// Shopee ไม่ support standard OAuth state param — ใช้ ts+sig แทน
    /// </summary>
    internal static async Task<IResult> AuthorizeAsync(
        string? tenant_id,
        IAuthorizeShopeeUseCase authorizeUseCase,
        IOptions<ShopeeOptions> options,
        CancellationToken ct = default)
    {
        if (!Guid.TryParse(tenant_id, out var tenantId))
            return Results.BadRequest();

        var ts = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
        var sig = ComputeStateSignature(tenantId.ToString(), ts, options.Value.OAuthStateSecret);

        var callbackBase = options.Value.OAuthRedirectUrl;
        var redirectUrl = $"{callbackBase}?tenant_id={tenantId}&ts={ts}&sig={sig}";

        var authUrl = await authorizeUseCase.ExecuteAsync(redirectUrl, ct);
        return Results.Redirect(authUrl);
    }

    /// <summary>
    /// Shopee redirects here after user grants permission:
    /// GET /api/shopee/oauth/callback?tenant_id={uuid}&ts={unix}&sig={hmac}&code={code}&shop_id={shopId}
    /// </summary>
    internal static async Task<IResult> CallbackAsync(
        HttpContext ctx,
        string? tenant_id,
        string? ts,
        string? sig,
        string? code,
        long? shop_id,
        IHandleShopeeCallbackUseCase callbackUseCase,
        IOptions<ShopeeOptions> options,
        CancellationToken ct = default)
    {
        if (!Guid.TryParse(tenant_id, out var tenantId))
            return Results.BadRequest();

        if (string.IsNullOrEmpty(code))
            return Results.BadRequest();

        if (shop_id is null or 0)
            return Results.BadRequest();

        if (!ValidateStateSignature(tenantId.ToString(), ts, sig, options.Value))
            return Results.BadRequest();

        try
        {
            await callbackUseCase.ExecuteAsync(tenantId, code, shop_id.Value, ct);
        }
        catch (Exception)
        {
            return Results.Redirect($"/settings/connections/shopee?error=oauth_failed");
        }

        return Results.Redirect($"/settings/connections/shopee?step=2");
    }

    // ─── Helpers ──────────────────────────────────────────────────────────────

    private static string ComputeStateSignature(string tenantId, string ts, string secret)
    {
        var message = $"{tenantId}:{ts}";
        var keyBytes = Encoding.UTF8.GetBytes(secret.Length > 0 ? secret : "dev-insecure-placeholder");
        var msgBytes = Encoding.UTF8.GetBytes(message);
        var hash = HMACSHA256.HashData(keyBytes, msgBytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static bool ValidateStateSignature(string tenantId, string? ts, string? sig, ShopeeOptions opts)
    {
        if (string.IsNullOrEmpty(ts) || string.IsNullOrEmpty(sig))
            return false;

        if (!long.TryParse(ts, out var tsUnix))
            return false;

        var ageSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds() - tsUnix;
        if (ageSeconds < 0 || ageSeconds > opts.OAuthStateMaxAgeSeconds)
            return false;

        var expected = ComputeStateSignature(tenantId, ts, opts.OAuthStateSecret);
        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(expected),
            Encoding.UTF8.GetBytes(sig));
    }
}
