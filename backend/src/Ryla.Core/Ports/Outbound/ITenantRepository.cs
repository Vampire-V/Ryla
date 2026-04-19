using Ryla.Core.Domain;

namespace Ryla.Core.Ports.Outbound;

/// <summary>
/// Port สำหรับอ่านข้อมูล tenant
/// Write operations ทำผ่าน Supabase Auth trigger (handle_new_user)
/// </summary>
public interface ITenantRepository
{
    /// <summary>ดึง tenant จาก ID</summary>
    Task<Tenant?> GetByIdAsync(Guid tenantId, CancellationToken cancellationToken = default);

    /// <summary>ดึง tenant ที่ user เป็นสมาชิก (ผ่าน profiles table)</summary>
    Task<Tenant?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>ดึง profile ของ user</summary>
    Task<UserProfile?> GetProfileByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
}
