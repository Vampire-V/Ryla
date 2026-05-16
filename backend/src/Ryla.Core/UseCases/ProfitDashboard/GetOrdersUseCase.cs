using Ryla.Core.Ports.Outbound;

namespace Ryla.Core.UseCases.ProfitDashboard;

internal sealed class GetOrdersUseCase(IOrderRepository orderRepo) : IGetOrdersUseCase
{
    public Task<OrdersPage> ExecuteAsync(
        Guid tenantId,
        DateTimeOffset from,
        DateTimeOffset to,
        int page,
        int pageSize,
        CancellationToken ct = default)
        => orderRepo.GetOrdersAsync(tenantId, from, to, page, pageSize, ct);
}
