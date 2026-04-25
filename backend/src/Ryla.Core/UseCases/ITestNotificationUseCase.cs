namespace Ryla.Core.UseCases;

public sealed record TestNotificationResult(bool Success, string? ErrorMessage = null);

public interface ITestNotificationUseCase
{
    Task<TestNotificationResult> ExecuteAsync(Guid tenantId, CancellationToken ct = default);
}
