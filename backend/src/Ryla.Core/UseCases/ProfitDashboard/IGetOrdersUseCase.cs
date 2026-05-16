using Ryla.Core.Domain.Orders;
using Ryla.Core.Ports.Outbound;

namespace Ryla.Core.UseCases.ProfitDashboard;

public interface IGetOrdersUseCase
{
    Task<OrdersPage> ExecuteAsync(
        Guid tenantId,
        DateTimeOffset from,
        DateTimeOffset to,
        int page,
        int pageSize,
        CancellationToken ct = default);
}
