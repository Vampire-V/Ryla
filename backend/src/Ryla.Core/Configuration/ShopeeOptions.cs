namespace Ryla.Core.Configuration;

/// <summary>
/// Shopee Open Platform credentials + webhook config
/// ตั้งค่าผ่าน appsettings.json หรือ dotnet user-secrets
/// </summary>
public sealed class ShopeeOptions
{
    public const string SectionName = "Shopee";

    /// <summary>Query parameter name ที่ Shopee ส่ง HMAC signature</summary>
    public const string SignatureQueryParam = "authorization";

    /// <summary>Partner Key จาก Shopee Partner Console</summary>
    public string PartnerKey { get; set; } = string.Empty;

    /// <summary>
    /// Callback URL ที่ลงทะเบียนกับ Shopee
    /// ใช้ร่วมกับ raw body เพื่อคำนวณ HMAC base string
    /// </summary>
    public string CallbackUrl { get; set; } = string.Empty;

    /// <summary>อายุสูงสุดของ webhook request (วินาที) เพื่อป้องกัน replay attack</summary>
    public int MaxAgeSeconds { get; set; } = 300; // 5 minutes

    // ─── Finance API (RYLA-9) ─────────────────────────────────────────────────

    /// <summary>Partner ID จาก Shopee Partner Console (ใช้ใน API signature)</summary>
    public long PartnerId { get; set; }

    /// <summary>Base URL ของ Shopee Open Platform API</summary>
    public string ApiBaseUrl { get; set; } = "https://partner.shopeemobile.com";

    /// <summary>Redirect URL หลัง OAuth consent (ต่างจาก webhook CallbackUrl)</summary>
    public string OAuthRedirectUrl { get; set; } = string.Empty;

    /// <summary>
    /// Secret สำหรับ HMAC-sign OAuth state param (ป้องกัน CSRF)
    /// ต้องตั้งค่าใน user-secrets / Key Vault — ห้าม hardcode
    /// </summary>
    public string OAuthStateSecret { get; set; } = string.Empty;

    /// <summary>อายุสูงสุดของ OAuth state (วินาที) ก่อนถือว่า expired</summary>
    public int OAuthStateMaxAgeSeconds { get; set; } = 600; // 10 minutes
}
