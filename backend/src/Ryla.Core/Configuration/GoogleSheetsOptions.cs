namespace Ryla.Core.Configuration;

/// <summary>
/// Configuration สำหรับ Google Sheets API
/// ใช้ { get; set; } เท่านั้น — { get; init; } silent-fail กับ ConfigurationBinder (CLAUDE.md fix a735fb8)
/// </summary>
public sealed class GoogleSheetsOptions
{
    public const string SectionName = "GoogleSheets";

    /// <summary>
    /// Base URL ของ Google Sheets API
    /// ปกติ https://sheets.googleapis.com — override ใน dev/E2E เพื่อชี้ไปที่ stub
    /// </summary>
    public string BaseUrl { get; set; } = "https://sheets.googleapis.com";

    /// <summary>
    /// OAuth2 token endpoint สำหรับ service account JWT exchange
    /// </summary>
    public string TokenEndpoint { get; set; } = "https://oauth2.googleapis.com/token";
}
