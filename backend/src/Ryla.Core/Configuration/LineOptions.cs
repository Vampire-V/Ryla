namespace Ryla.Core.Configuration;

/// <summary>
/// Configuration สำหรับ LINE Messaging API
/// </summary>
public sealed class LineOptions
{
    public const string SectionName = "Line";

    /// <summary>
    /// Base URL ของ LINE Messaging API
    /// ปกติ https://api.line.me — override ใน dev/E2E เพื่อชี้ไปที่ stub
    /// </summary>
    public string BaseUrl { get; set; } = "https://api.line.me";
}
