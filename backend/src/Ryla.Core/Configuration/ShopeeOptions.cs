namespace Ryla.Core.Configuration;

/// <summary>
/// Shopee Open Platform credentials + webhook config
/// ตั้งค่าผ่าน appsettings.json หรือ dotnet user-secrets
/// </summary>
public sealed class ShopeeOptions
{
    public const string SectionName = "Shopee";

    /// <summary>Partner Key จาก Shopee Partner Console</summary>
    public string PartnerKey { get; set; } = string.Empty;

    /// <summary>
    /// Callback URL ที่ลงทะเบียนกับ Shopee
    /// ใช้ร่วมกับ raw body เพื่อคำนวณ HMAC base string
    /// </summary>
    public string CallbackUrl { get; set; } = string.Empty;

    /// <summary>อายุสูงสุดของ webhook request (วินาที) เพื่อป้องกัน replay attack</summary>
    public int MaxAgeSeconds { get; set; } = 300; // 5 minutes
}
