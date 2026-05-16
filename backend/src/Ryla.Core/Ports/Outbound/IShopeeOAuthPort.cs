namespace Ryla.Core.Ports.Outbound;

/// <summary>
/// Port สำหรับ Shopee OAuth 2.0 flow
/// </summary>
public interface IShopeeOAuthPort
{
    /// <summary>
    /// สร้าง Authorization URL สำหรับ redirect user ไป Shopee consent page
    /// </summary>
    Task<string> GenerateAuthorizationUrlAsync(
        string redirectUrl,
        CancellationToken ct = default);

    /// <summary>
    /// แลก authorization code → access_token + refresh_token
    /// </summary>
    Task<ShopeeTokenResponse> ExchangeCodeAsync(
        string code,
        long shopId,
        CancellationToken ct = default);

    /// <summary>
    /// Refresh access_token ด้วย refresh_token (access_token หมดอายุทุก 4 ชม.)
    /// </summary>
    Task<ShopeeRefreshResponse> RefreshAccessTokenAsync(
        long shopId,
        string refreshToken,
        CancellationToken ct = default);
}

public sealed record ShopeeTokenResponse(
    string AccessToken,
    string RefreshToken,
    DateTimeOffset ExpiresAt,
    long ShopId);

public sealed record ShopeeRefreshResponse(
    string NewAccessToken,
    DateTimeOffset NewExpiresAt);
