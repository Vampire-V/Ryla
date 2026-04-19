namespace Ryla.Core.Configuration;

/// <summary>
/// TikTok Shop Partner API credentials และ webhook configuration
/// ตั้งค่าผ่าน appsettings.json หรือ dotnet user-secrets
/// </summary>
public sealed class TikTokShopOptions
{
    public const string SectionName = "TikTokShop";

    /// <summary>ชื่อ header ที่ TikTok Shop ใส่ HMAC signature</summary>
    public const string SignatureHeaderName = "TikTok-Signature";

    /// <summary>Client Secret จาก TikTok Shop Partner Center</summary>
    public string ClientSecret { get; set; } = string.Empty;

    /// <summary>
    /// ชื่อ HTTP header ที่ TikTok Shop ส่ง HMAC signature มาด้วย
    /// Default: "TikTok-Signature" (format: t={timestamp},s={hex_signature})
    /// </summary>
    public string SignatureHeader { get; set; } = SignatureHeaderName;

    /// <summary>อายุสูงสุดของ webhook request (วินาที) เพื่อป้องกัน replay attack</summary>
    public int MaxAgeSeconds { get; set; } = 300; // 5 minutes
}
