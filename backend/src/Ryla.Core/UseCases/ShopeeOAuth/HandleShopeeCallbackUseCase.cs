using Microsoft.Extensions.Logging;
using Ryla.Core.Ports.Outbound;

namespace Ryla.Core.UseCases.ShopeeOAuth;

internal sealed class HandleShopeeCallbackUseCase(
    IShopeeOAuthPort oauthPort,
    IShopeeTokenPort tokenPort,
    ILogger<HandleShopeeCallbackUseCase> logger) : IHandleShopeeCallbackUseCase
{
    public async Task ExecuteAsync(
        Guid tenantId,
        string code,
        long shopId,
        CancellationToken ct = default)
    {
        var response = await oauthPort.ExchangeCodeAsync(code, shopId, ct);

        await tokenPort.SaveTokenAsync(
            tenantId,
            response.ShopId,
            response.AccessToken,
            response.RefreshToken,
            response.ExpiresAt,
            ct);

        logger.LogInformation(
            "Shopee OAuth complete for tenant={TenantId} shopId={ShopId}",
            tenantId, response.ShopId);
    }
}
