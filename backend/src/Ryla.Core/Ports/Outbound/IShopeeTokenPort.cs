using Ryla.Core.Domain.Orders;

namespace Ryla.Core.Ports.Outbound;

/// <summary>
/// Port สำหรับ shopee_tokens table — OAuth token storage ผ่าน Supabase Vault
/// </summary>
public interface IShopeeTokenPort
{
    /// <summary>
    /// ดึง decrypted token สำหรับ tenant (null ถ้าไม่ได้ connect หรือ auth_error)
    /// </summary>
    Task<ShopeeTokenSecrets?> GetTokenAsync(
        Guid tenantId,
        CancellationToken ct = default);

    /// <summary>
    /// บันทึก token ใหม่หลัง OAuth callback
    /// </summary>
    Task SaveTokenAsync(
        Guid tenantId,
        long shopId,
        string accessToken,
        string refreshToken,
        DateTimeOffset expiresAt,
        CancellationToken ct = default);

    /// <summary>
    /// อัปเดต access_token หลัง refresh
    /// </summary>
    Task UpdateAccessTokenAsync(
        Guid tenantId,
        string newAccessToken,
        DateTimeOffset newExpiresAt,
        CancellationToken ct = default);

    /// <summary>
    /// ดึง tenant IDs ทั้งหมดที่มี active token (สำหรับ Azure Function scan)
    /// </summary>
    Task<IReadOnlyList<Guid>> GetTenantIdsWithTokenAsync(
        CancellationToken ct = default);

    /// <summary>
    /// ระบุ tenant จาก shop_id (สำหรับ webhook → store order ใน orders table)
    /// </summary>
    Task<Guid?> GetTenantIdByShopIdAsync(
        long shopId,
        CancellationToken ct = default);
}
