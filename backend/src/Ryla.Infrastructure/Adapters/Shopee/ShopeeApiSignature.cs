using System.Security.Cryptography;
using System.Text;

namespace Ryla.Infrastructure.Adapters.Shopee;

/// <summary>
/// Shopee Open Platform API signature helper
/// Ref: https://open.shopee.com/documents/v2/v2.account.api-signature
/// </summary>
internal static class ShopeeApiSignature
{
    /// <summary>
    /// Partner-level signature (auth + token endpoints)
    /// base_string = partner_id + path + timestamp
    /// </summary>
    public static string ComputePartnerSign(long partnerId, string path, long timestamp, string partnerKey)
    {
        var baseString = $"{partnerId}{path}{timestamp}";
        return ComputeHmac(partnerKey, baseString);
    }

    /// <summary>
    /// Shop-level signature (order + shop endpoints with access_token)
    /// base_string = partner_id + path + timestamp + access_token + shop_id
    /// </summary>
    public static string ComputeShopSign(
        long partnerId, string path, long timestamp,
        string accessToken, long shopId, string partnerKey)
    {
        var baseString = $"{partnerId}{path}{timestamp}{accessToken}{shopId}";
        return ComputeHmac(partnerKey, baseString);
    }

    public static long UnixTimestamp() =>
        DateTimeOffset.UtcNow.ToUnixTimeSeconds();

    private static string ComputeHmac(string key, string data)
    {
        var keyBytes = Encoding.UTF8.GetBytes(key);
        var dataBytes = Encoding.UTF8.GetBytes(data);
        var hash = HMACSHA256.HashData(keyBytes, dataBytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
