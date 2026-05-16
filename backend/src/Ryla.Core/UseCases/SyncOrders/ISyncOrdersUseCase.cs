namespace Ryla.Core.UseCases.SyncOrders;

/// <summary>
/// Use case: Azure Function Timer → poll COMPLETED pending orders → fill financial data
/// </summary>
public interface ISyncOrdersUseCase
{
    Task<SyncOrdersResult> ExecuteAsync(CancellationToken ct = default);
}

public sealed record SyncOrdersResult(int Processed, int Errors, int AuthErrors);
