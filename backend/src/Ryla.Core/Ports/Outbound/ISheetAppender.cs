using Ryla.Core.Domain.Integrations;

namespace Ryla.Core.Ports.Outbound;

/// <summary>
/// Port สำหรับ append row ไปยัง Google Sheets
/// Zero external dependencies — ไม่มี Google SDK ใน Core
/// </summary>
public interface ISheetAppender
{
    Task<SheetAppendResult> AppendRowAsync(
        GoogleSheetsCredentials creds,
        IReadOnlyList<string> values,
        CancellationToken ct = default);
}

/// <summary>
/// Result ของการ append row — Result pattern, ไม่ throw exception สำหรับ business outcomes
/// </summary>
public sealed record SheetAppendResult(
    bool Success,
    string? ErrorCode = null,
    string? ErrorMessage = null);
