using Ryla.Core.Domain.Orders;
using Ryla.Core.Ports.Outbound;

namespace Ryla.Core.UseCases.ProfitDashboard;

internal sealed class GetProfitSummaryUseCase(IOrderRepository orderRepo)
    : IGetProfitSummaryUseCase
{
    public Task<ProfitSummary> ExecuteAsync(
        Guid tenantId,
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken ct = default)
        => orderRepo.GetSummaryAsync(tenantId, from, to, ct);
}
