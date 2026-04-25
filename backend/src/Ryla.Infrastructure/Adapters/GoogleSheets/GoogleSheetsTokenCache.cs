using System.Collections.Concurrent;

namespace Ryla.Infrastructure.Adapters.GoogleSheets;

/// <summary>
/// In-memory access-token cache สำหรับ Google Sheets adapter
/// แยกออกจาก GoogleSheetsClient เพราะ typed HttpClient ลงทะเบียนเป็น transient
/// (instance ใหม่ทุก request) — singleton นี้ทำให้ token ถูก reuse ข้าม request
/// AOT-safe: ใช้ ConcurrentDictionary, ไม่มี reflection
/// </summary>
internal sealed class GoogleSheetsTokenCache
{
    private readonly ConcurrentDictionary<string, (string Token, DateTimeOffset ExpiresAt)> _entries = new();

    public bool TryGet(string clientEmail, DateTimeOffset now, TimeSpan minRemaining, out string token)
    {
        if (_entries.TryGetValue(clientEmail, out var entry) && entry.ExpiresAt - now > minRemaining)
        {
            token = entry.Token;
            return true;
        }
        token = string.Empty;
        return false;
    }

    public void Store(string clientEmail, string token, DateTimeOffset expiresAt)
    {
        _entries[clientEmail] = (token, expiresAt);
    }
}
