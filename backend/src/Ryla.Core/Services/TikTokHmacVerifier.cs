using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Ryla.Core.Configuration;

namespace Ryla.Core.Services;

/// <summary>
/// ตรวจสอบ HMAC-SHA256 signature ของ TikTok Shop webhook
/// Algorithm: HMAC-SHA256(key=clientSecret, data="{timestamp}.{rawBody}")
/// Header format: "t={unixTimestamp},s={hexSignature}"
/// </summary>
public sealed class TikTokHmacVerifier : ITikTokHmacVerifier
{
    private readonly TikTokShopOptions _options;

    public TikTokHmacVerifier(IOptions<TikTokShopOptions> options)
        => _options = options.Value;

    /// <inheritdoc />
    public bool Verify(string rawBody, string signatureHeader)
        => VerifyCore(rawBody, signatureHeader, _options.ClientSecret,
            DateTimeOffset.UtcNow.ToUnixTimeSeconds(), _options.MaxAgeSeconds);

    /// <summary>
    /// Pure static method สำหรับ unit testing (ไม่ต้องมี DI)
    /// </summary>
    internal static bool VerifyCore(
        string rawBody,
        string signatureHeader,
        string clientSecret,
        long nowUnixSeconds,
        int maxAgeSeconds = 300)
    {
        if (string.IsNullOrEmpty(signatureHeader)) return false;
        if (string.IsNullOrEmpty(rawBody)) return false;

        // Parse "t={timestamp},s={hex_signature}"
        string? timestamp = null;
        string? signature = null;

        foreach (var part in signatureHeader.Split(',', StringSplitOptions.RemoveEmptyEntries))
        {
            if (part.StartsWith("t=", StringComparison.Ordinal))
                timestamp = part[2..];
            else if (part.StartsWith("s=", StringComparison.Ordinal))
                signature = part[2..];
        }

        if (timestamp is null || signature is null) return false;
        if (!long.TryParse(timestamp, out var ts)) return false;

        // Replay attack protection: reject requests older than maxAgeSeconds
        if (Math.Abs(nowUnixSeconds - ts) > maxAgeSeconds) return false;

        // Compute HMAC-SHA256: key=clientSecret, data="{timestamp}.{rawBody}"
        var payload = $"{timestamp}.{rawBody}";
        var keyBytes = Encoding.UTF8.GetBytes(clientSecret);
        var payloadBytes = Encoding.UTF8.GetBytes(payload);

        using var hmac = new HMACSHA256(keyBytes);
        var computed = hmac.ComputeHash(payloadBytes);
        var computedHex = Convert.ToHexString(computed).ToLowerInvariant();

        // ใช้ constant-time comparison เพื่อป้องกัน timing attack
        return CryptographicOperations.FixedTimeEquals(
            Encoding.ASCII.GetBytes(computedHex),
            Encoding.ASCII.GetBytes(signature));
    }
}
