using Ryla.Core.Common;

namespace Ryla.Core.Ports.Outbound;

/// <summary>
/// Port สำหรับ resolve tenant_id จาก user_id (Supabase auth sub claim)
/// ใช้ใน request pipeline เพื่อแก้ปัญหา IDOR — ดึง tenant จาก JWT sub ไม่ใช่ header
/// </summary>
public interface ITenantResolver
{
    /// <summary>
    /// Resolve tenant_id จาก user_id (sub claim ใน Supabase JWT)
    /// </summary>
    /// <param name="userId">User UUID จาก JWT sub claim</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Result.Ok(tenantId) ถ้าหา profile เจอ, Result.Fail ถ้าไม่พบ</returns>
    ValueTask<Result<Guid>> ResolveAsync(Guid userId, CancellationToken ct = default);
}
