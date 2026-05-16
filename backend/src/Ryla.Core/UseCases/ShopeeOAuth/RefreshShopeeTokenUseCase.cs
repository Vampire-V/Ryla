using Microsoft.Extensions.Logging;
using Ryla.Core.Ports.Outbound;

namespace Ryla.Core.UseCases.ShopeeOAuth;

internal sealed class RefreshShopeeTokenUseCase(
    IShopeeTokenPort tokenPort,
    IShopeeOAuthPort oauthPort,
    ILogger<RefreshShopeeTokenUseCase> logger) : IRefreshShopeeTokenUseCase
{
    private static readonly TimeSpan RefreshThreshold = TimeSpan.FromMinutes(30);

    public async Task<RefreshResult> ExecuteAsync(
        Guid tenantId,
        CancellationToken ct = default)
    {
        var secrets = await tokenPort.GetTokenAsync(tenantId, ct);
        if (secrets is null)
            return RefreshResult.NotRequired;

        if (!secrets.IsExpiringSoon(DateTimeOffset.UtcNow, RefreshThreshold))
            return RefreshResult.NotRequired;

        try
        {
            var response = await oauthPort.RefreshAccessTokenAsync(secrets.ShopId, secrets.RefreshToken, ct);
            await tokenPort.UpdateAccessTokenAsync(tenantId, response.NewAccessToken, response.NewExpiresAt, ct);

            logger.LogInformation("Shopee token refreshed for tenant={TenantId}", tenantId);
            return RefreshResult.Refreshed;
        }
        catch (ShopeeAuthExpiredException)
        {
            logger.LogWarning("Shopee refresh_token expired for tenant={TenantId}", tenantId);
            return RefreshResult.AuthExpired;
        }
    }
}

/// <summary>
/// throw เมื่อ Shopee ตอบว่า refresh_token หมดอายุ (ต้อง re-OAuth)
/// </summary>
public sealed class ShopeeAuthExpiredException() : Exception("Shopee refresh token expired — re-authorization required");
