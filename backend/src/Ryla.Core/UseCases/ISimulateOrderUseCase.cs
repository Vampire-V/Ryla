using Ryla.Core.Domain.Webhooks;

namespace Ryla.Core.UseCases;

public sealed record SimulateOrderResult(
    bool LineSuccess,
    string? LineError,
    bool SheetsSuccess,
    string? SheetsError,
    string OrderId);

public interface ISimulateOrderUseCase
{
    Task<SimulateOrderResult> ExecuteAsync(
        Guid tenantId,
        Platform platform,
        CancellationToken ct = default);
}
