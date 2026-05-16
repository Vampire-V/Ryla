using Ryla.Core.Domain.Orders;

namespace Ryla.Core.UseCases.SkuCosts;

public interface IGetSkuCostsUseCase
{
    Task<IReadOnlyList<SkuCost>> ExecuteAsync(
        Guid tenantId,
        string platform,
        CancellationToken ct = default);
}
