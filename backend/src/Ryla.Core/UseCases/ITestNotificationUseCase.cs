namespace Ryla.Core.UseCases;

public record TestNotificationResult(bool Success, string? ErrorMessage = null);

public interface ITestNotificationUseCase
{
    Task<TestNotificationResult> ExecuteAsync(Guid tenantId, CancellationToken ct = default);
}
