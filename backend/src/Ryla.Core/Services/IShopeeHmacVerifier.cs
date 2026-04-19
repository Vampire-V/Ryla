namespace Ryla.Core.Services;

/// <summary>
/// ตรวจสอบ HMAC signature ของ Shopee webhook
/// </summary>
public interface IShopeeHmacVerifier
{
    /// <summary>
    /// Returns true ถ้า signature valid
    /// Signature = HMAC-SHA256(partnerKey, callbackUrl + rawBody)
    /// </summary>
    bool Verify(string rawBody, string authorizationSignature);
}
