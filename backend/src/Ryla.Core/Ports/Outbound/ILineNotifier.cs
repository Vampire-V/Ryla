namespace Ryla.Core.Ports.Outbound;

/// <summary>
/// Port สำหรับส่ง LINE push message
/// Zero external dependencies — ไม่มี LINE SDK ใน Core
/// </summary>
public interface ILineNotifier
{
    Task<LineNotifyResult> PushAsync(
        string channelAccessToken,
        string targetUserId,
        string message,
        CancellationToken ct = default);
}

/// <summary>
/// Result ของการส่ง LINE notification
/// ใช้ Result pattern — ไม่ throw exception สำหรับ business outcomes
/// </summary>
public sealed record LineNotifyResult(
    bool Success,
    string? ErrorCode = null,
    string? ErrorMessage = null);
