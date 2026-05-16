using Ryla.Core.Domain.Orders;
using Ryla.Core.Ports.Outbound;

namespace Ryla.Core.UseCases.SkuCosts;

internal sealed class UpsertSkuCostUseCase(ISkuCostRepository skuCostRepo) : IUpsertSkuCostUseCase
{
    public Task<SkuCost> ExecuteAsync(
        Guid tenantId,
        string platform,
        string itemSku,
        string? itemName,
        decimal cogs,
        CancellationToken ct = default)
        => skuCostRepo.UpsertAsync(tenantId, platform, itemSku, itemName, cogs, ct);
}
