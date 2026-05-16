using Ryla.Core.Domain.Orders;

namespace Ryla.Core.UseCases.SkuCosts;

public interface IUpsertSkuCostUseCase
{
    Task<SkuCost> ExecuteAsync(
        Guid tenantId,
        string platform,
        string itemSku,
        string? itemName,
        decimal cogs,
        CancellationToken ct = default);
}
