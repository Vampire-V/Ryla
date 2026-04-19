namespace Ryla.Core.Services;

/// <summary>
/// ตรวจสอบ HMAC signature ของ TikTok Shop webhook
/// </summary>
public interface ITikTokHmacVerifier
{
    /// <summary>
    /// Returns true ถ้า signature valid และ request ไม่เกิน MaxAgeSeconds
    /// </summary>
    bool Verify(string rawBody, string signatureHeader);
}
