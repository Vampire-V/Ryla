using Ryla.Core.Ports.Outbound;

namespace Ryla.Core.UseCases.ProfitDashboard;

internal sealed class GetSyncStatusUseCase(IOrderRepository orderRepo) : IGetSyncStatusUseCase
{
    public Task<SyncProgress> ExecuteAsync(
        Guid tenantId,
        CancellationToken ct = default)
        => orderRepo.GetSyncProgressAsync(tenantId, ct);
}
