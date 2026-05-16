using Ryla.Core.Domain.Orders;
using Ryla.Core.Ports.Outbound;

namespace Ryla.Core.UseCases.SkuCosts;

internal sealed class GetSkuCostsUseCase(ISkuCostRepository skuCostRepo) : IGetSkuCostsUseCase
{
    public Task<IReadOnlyList<SkuCost>> ExecuteAsync(
        Guid tenantId,
        string platform,
        CancellationToken ct = default)
        => skuCostRepo.GetByTenantAsync(tenantId, platform, ct);
}
