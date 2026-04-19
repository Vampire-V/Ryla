using Ryla.Core.Domain.Webhooks;

namespace Ryla.Core.UseCases;

/// <summary>
/// Use case port สำหรับ process order webhook event
/// </summary>
public interface IProcessOrderWebhookUseCase
{
    Task<ProcessOrderResult> ExecuteAsync(
        OrderWebhookContext ctx,
        CancellationToken ct = default);
}

/// <summary>
/// Status ของการ process — ใช้แยกแต่ละ skip reason เพื่อ observability
/// </summary>
public enum ProcessOrderStatus
{
    Sent,
    SkippedNoTenant,
    SkippedNoLineConfig,
    FailedLineDelivery
}

/// <summary>
/// Result ของ use case — Result pattern, ไม่ throw exception
/// </summary>
public sealed record ProcessOrderResult(
    ProcessOrderStatus Status,
    string? Detail = null);
