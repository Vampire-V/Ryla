using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Ryla.Core.Configuration;

namespace Ryla.Core.Services;

/// <summary>
/// ตรวจสอบ HMAC-SHA256 signature ของ Shopee webhook
/// Algorithm: HMAC-SHA256(key=partnerKey, data=callbackUrl+rawBody)
/// Signature มาทาง query parameter ?authorization={hex}
/// </summary>
public sealed class ShopeeHmacVerifier : IShopeeHmacVerifier
{
    private readonly ShopeeOptions _options;

    public ShopeeHmacVerifier(IOptions<ShopeeOptions> options)
        => _options = options.Value;

    /// <inheritdoc />
    public bool Verify(string rawBody, string authorizationSignature)
        => VerifyCore(rawBody, authorizationSignature,
            _options.PartnerKey, _options.CallbackUrl);

    /// <summary>
    /// Pure static method สำหรับ unit testing (ไม่ต้องมี DI)
    /// </summary>
    internal static bool VerifyCore(
        string rawBody,
        string authorizationSignature,
        string partnerKey,
        string callbackUrl)
    {
        if (string.IsNullOrEmpty(authorizationSignature)) return false;
        if (string.IsNullOrEmpty(rawBody)) return false;
        if (string.IsNullOrEmpty(partnerKey)) return false;

        var baseString = callbackUrl + rawBody;
        var keyBytes = Encoding.UTF8.GetBytes(partnerKey);
        var dataBytes = Encoding.UTF8.GetBytes(baseString);

        using var hmac = new HMACSHA256(keyBytes);
        var computed = hmac.ComputeHash(dataBytes);
        var computedHex = Convert.ToHexString(computed).ToLowerInvariant();

        return CryptographicOperations.FixedTimeEquals(
            Encoding.ASCII.GetBytes(computedHex),
            Encoding.ASCII.GetBytes(authorizationSignature.ToLowerInvariant()));
    }
}
