namespace Ryla.Core.Domain.Orders;

/// <summary>
/// Shopee OAuth token ที่ decrypt มาจาก Supabase Vault แล้ว
/// ใช้สำหรับเรียก Shopee Finance API
/// </summary>
public sealed record ShopeeTokenSecrets(
    Guid TenantId,
    long ShopId,
    string AccessToken,
    string RefreshToken,
    DateTimeOffset ExpiresAt)
{
    public bool IsExpiringSoon(DateTimeOffset now, TimeSpan threshold)
        => ExpiresAt - now < threshold;
}
