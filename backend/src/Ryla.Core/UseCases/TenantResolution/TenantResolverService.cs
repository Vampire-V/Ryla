using Ryla.Core.Common;
using Ryla.Core.Ports.Outbound;

namespace Ryla.Core.UseCases.TenantResolution;

/// <summary>
/// Resolves tenant_id จาก user_id โดย delegate ไปที่ ITenantRepository
/// ใช้ใน request pipeline เพื่อแก้ IDOR vulnerability
/// </summary>
internal sealed class TenantResolverService(ITenantRepository tenantRepository) : ITenantResolver
{
    private static readonly Error NotFoundError =
        new("TENANT_NOT_FOUND", "No tenant associated with this user");

    public async ValueTask<Result<Guid>> ResolveAsync(Guid userId, CancellationToken ct = default)
    {
        var profile = await tenantRepository.GetProfileByUserIdAsync(userId, ct);

        return profile is null
            ? Result<Guid>.Fail(NotFoundError)
            : Result<Guid>.Ok(profile.TenantId);
    }
}
