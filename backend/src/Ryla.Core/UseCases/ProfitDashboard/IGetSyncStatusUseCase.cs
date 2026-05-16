using Ryla.Core.Ports.Outbound;

namespace Ryla.Core.UseCases.ProfitDashboard;

public interface IGetSyncStatusUseCase
{
    Task<SyncProgress> ExecuteAsync(
        Guid tenantId,
        CancellationToken ct = default);
}
