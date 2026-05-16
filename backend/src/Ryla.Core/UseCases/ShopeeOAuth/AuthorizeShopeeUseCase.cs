using Ryla.Core.Ports.Outbound;

namespace Ryla.Core.UseCases.ShopeeOAuth;

internal sealed class AuthorizeShopeeUseCase(IShopeeOAuthPort oauthPort) : IAuthorizeShopeeUseCase
{
    public Task<string> ExecuteAsync(
        string redirectUrl,
        CancellationToken ct = default)
        => oauthPort.GenerateAuthorizationUrlAsync(redirectUrl, ct);
}
