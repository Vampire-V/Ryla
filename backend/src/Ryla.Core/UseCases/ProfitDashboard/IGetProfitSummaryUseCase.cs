using Ryla.Core.Domain.Orders;

namespace Ryla.Core.UseCases.ProfitDashboard;

public interface IGetProfitSummaryUseCase
{
    Task<ProfitSummary> ExecuteAsync(
        Guid tenantId,
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken ct = default);
}
