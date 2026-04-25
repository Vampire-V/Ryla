using Ryla.Core.Domain.Integrations;
using Ryla.Core.Domain.Webhooks;

namespace Ryla.Core.Ports.Outbound;

/// <summary>
/// Port สำหรับ query connections table
/// </summary>
public interface IConnectionRepository
{
    /// <summary>
    /// หา tenant_id จาก shop_id และ platform
    /// คืน null ถ้าไม่พบหรือ is_active = false
    /// </summary>
    Task<Guid?> FindTenantIdByShopIdAsync(
        Platform platform,
        string shopId,
        CancellationToken ct = default);

    /// <summary>
    /// ดึง LINE OA credentials ของ tenant
    /// คืน null ถ้าไม่มี line_oa connection หรือ token field หายไป
    /// </summary>
    Task<LineCredentials?> GetLineCredentialsAsync(
        Guid tenantId,
        CancellationToken ct = default);

    /// <summary>
    /// ดึง Google Sheets credentials ของ tenant
    /// คืน null ถ้าไม่มี google_sheets connection หรือ field หายไป
    /// </summary>
    Task<GoogleSheetsCredentials?> GetGoogleSheetsCredentialsAsync(
        Guid tenantId,
        CancellationToken ct = default);
}
