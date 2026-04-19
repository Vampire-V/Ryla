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
/// Status โดยรวมของการ process — backward-compatible กับ API response
/// </summary>
public enum ProcessOrderStatus
{
    Sent,
    SkippedNoTenant,
    SkippedNoLineConfig,
    FailedLineDelivery
}

/// <summary>
/// Status ของ LINE delivery channel
/// </summary>
public enum LineDeliveryStatus
{
    Sent,
    SkippedNoConfig,
    Failed
}

/// <summary>
/// Status ของ Google Sheets delivery channel
/// </summary>
public enum SheetsDeliveryStatus
{
    Sent,
    SkippedNoConfig,
    SkippedNoTenant,
    Failed
}

/// <summary>
/// Result ของ use case — Result pattern, ไม่ throw exception
/// รองรับทั้ง LINE และ Sheets delivery status แยกกัน
/// </summary>
public sealed record ProcessOrderResult(
    ProcessOrderStatus Status,
    LineDeliveryStatus LineStatus = LineDeliveryStatus.SkippedNoConfig,
    SheetsDeliveryStatus SheetsStatus = SheetsDeliveryStatus.SkippedNoTenant,
    string? Detail = null);
