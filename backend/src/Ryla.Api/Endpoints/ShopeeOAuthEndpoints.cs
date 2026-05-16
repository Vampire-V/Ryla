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
            .AllowAnonymous() // Frontend redirect — ยังไม่มี JWT auth middleware
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
    /// Encodes tenant_id เข้าไปใน redirect URL (state pattern) เพื่อให้
    /// callback handler รู้ว่า tenant ไหน ทำการ authorize
    /// </summary>
    internal static async Task<IResult> AuthorizeAsync(
        string? tenant_id,
        IAuthorizeShopeeUseCase authorizeUseCase,
        IOptions<ShopeeOptions> options,
        CancellationToken ct = default)
    {
        if (!Guid.TryParse(tenant_id, out var tenantId))
            return Results.BadRequest();

        // Embed tenant_id ใน redirect URL — Shopee จะ append code + shop_id ต่อท้าย
        var callbackBase = options.Value.OAuthRedirectUrl;
        var redirectUrl = $"{callbackBase}?tenant_id={tenantId}";

        var authUrl = await authorizeUseCase.ExecuteAsync(redirectUrl, ct);
        return Results.Redirect(authUrl);
    }

    /// <summary>
    /// Shopee redirects here after user grants permission:
    /// GET /api/shopee/oauth/callback?tenant_id={uuid}&code={code}&shop_id={shopId}
    /// </summary>
    internal static async Task<IResult> CallbackAsync(
        HttpContext ctx,
        string? tenant_id,
        string? code,
        long? shop_id,
        IHandleShopeeCallbackUseCase callbackUseCase,
        CancellationToken ct = default)
    {
        if (!Guid.TryParse(tenant_id, out var tenantId))
            return Results.BadRequest();

        if (string.IsNullOrEmpty(code))
            return Results.BadRequest();

        if (shop_id is null or 0)
            return Results.BadRequest();

        try
        {
            await callbackUseCase.ExecuteAsync(tenantId, code, shop_id.Value, ct);
        }
        catch (Exception)
        {
            // Redirect to frontend with error — ไม่ expose exception detail ให้ browser
            return Results.Redirect($"/settings/connections/shopee?error=oauth_failed");
        }

        // Redirect ไปยัง onboarding wizard step 2 (sync progress)
        return Results.Redirect($"/settings/connections/shopee?step=2");
    }
}
