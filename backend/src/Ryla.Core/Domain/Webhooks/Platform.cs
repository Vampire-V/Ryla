namespace Ryla.Core.Domain.Webhooks;

/// <summary>
/// Inbound webhook source platform
/// Note: LineOa ไม่อยู่ใน enum นี้ — เป็น outbound notification, ไม่ใช่ inbound webhook source
/// </summary>
public enum Platform
{
    TikTokShop,
    Shopee
}
