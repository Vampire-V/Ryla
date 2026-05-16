using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Ryla.Core.Configuration;
using Ryla.Core.Ports.Outbound;
using Ryla.Core.UseCases.ShopeeOAuth;

namespace Ryla.Infrastructure.Adapters.Shopee;

/// <summary>
/// Shopee OAuth 2.0 adapter — token exchange, refresh, authorization URL
/// </summary>
internal sealed class ShopeeOAuthAdapter(
    HttpClient httpClient,
    IOptions<ShopeeOptions> options) : IShopeeOAuthPort
{
    private const string AuthPath = "/api/v2/shop/auth_partner";
    private const string TokenPath = "/api/v2/auth/token/get";
    private const string RefreshPath = "/api/v2/auth/access_token/get";

    public Task<string> GenerateAuthorizationUrlAsync(
        string redirectUrl,
        CancellationToken ct = default)
    {
        var opts = options.Value;
        var timestamp = ShopeeApiSignature.UnixTimestamp();
        var sign = ShopeeApiSignature.ComputePartnerSign(opts.PartnerId, AuthPath, timestamp, opts.PartnerKey);

        var url = $"{opts.ApiBaseUrl}{AuthPath}" +
                  $"?partner_id={opts.PartnerId}" +
                  $"&timestamp={timestamp}" +
                  $"&sign={sign}" +
                  $"&redirect={Uri.EscapeDataString(redirectUrl)}";

        return Task.FromResult(url);
    }

    public async Task<ShopeeTokenResponse> ExchangeCodeAsync(
        string code, long shopId, CancellationToken ct = default)
    {
        var opts = options.Value;
        var timestamp = ShopeeApiSignature.UnixTimestamp();
        var sign = ShopeeApiSignature.ComputePartnerSign(opts.PartnerId, TokenPath, timestamp, opts.PartnerKey);

        var url = $"{opts.ApiBaseUrl}{TokenPath}" +
                  $"?partner_id={opts.PartnerId}&timestamp={timestamp}&sign={sign}";

        var body = new ShopeeTokenExchangeRequest(code, shopId, opts.PartnerId);
        var json = JsonSerializer.Serialize(body, ShopeeApiJsonContext.Default.ShopeeTokenExchangeRequest);
        using var content = new StringContent(json, Encoding.UTF8, "application/json");

        using var response = await httpClient.PostAsync(url, content, ct);
        response.EnsureSuccessStatusCode();

        var responseJson = await response.Content.ReadAsStringAsync(ct);
        var result = JsonSerializer.Deserialize(responseJson, ShopeeApiJsonContext.Default.ShopeeTokenExchangeResponse)
            ?? throw new InvalidOperationException("Shopee token exchange: null response");

        if (!string.IsNullOrEmpty(result.Error) && result.Error != "")
            throw new ShopeeAuthExpiredException();

        var expiresAt = DateTimeOffset.UtcNow.AddSeconds(result.ExpireIn);
        return new ShopeeTokenResponse(result.AccessToken, result.RefreshToken, expiresAt, result.ShopId);
    }

    public async Task<ShopeeRefreshResponse> RefreshAccessTokenAsync(
        long shopId, string refreshToken, CancellationToken ct = default)
    {
        var opts = options.Value;
        var timestamp = ShopeeApiSignature.UnixTimestamp();
        var sign = ShopeeApiSignature.ComputePartnerSign(opts.PartnerId, RefreshPath, timestamp, opts.PartnerKey);

        var url = $"{opts.ApiBaseUrl}{RefreshPath}" +
                  $"?partner_id={opts.PartnerId}&timestamp={timestamp}&sign={sign}";

        var body = new ShopeeRefreshTokenRequest(refreshToken, shopId, opts.PartnerId);
        var json = JsonSerializer.Serialize(body, ShopeeApiJsonContext.Default.ShopeeRefreshTokenRequest);
        using var content = new StringContent(json, Encoding.UTF8, "application/json");

        using var response = await httpClient.PostAsync(url, content, ct);
        response.EnsureSuccessStatusCode();

        var responseJson = await response.Content.ReadAsStringAsync(ct);
        var result = JsonSerializer.Deserialize(responseJson, ShopeeApiJsonContext.Default.ShopeeRefreshTokenApiResponse)
            ?? throw new InvalidOperationException("Shopee refresh: null response");

        // "error_auth" = refresh_token expired or invalid
        if (result.Error?.Contains("auth", StringComparison.OrdinalIgnoreCase) == true)
            throw new ShopeeAuthExpiredException();

        var newExpiresAt = DateTimeOffset.UtcNow.AddSeconds(result.ExpireIn);
        return new ShopeeRefreshResponse(result.AccessToken, newExpiresAt);
    }
}
