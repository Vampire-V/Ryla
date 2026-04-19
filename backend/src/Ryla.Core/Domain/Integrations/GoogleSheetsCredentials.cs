namespace Ryla.Core.Domain.Integrations;

/// <summary>
/// Google Sheets credentials ที่ดึงมาจาก connections table
/// ServiceAccountJson คือ JSON string ของ service account key file จาก Google Cloud Console
/// </summary>
public sealed record GoogleSheetsCredentials(
    string SpreadsheetId,
    string WorksheetName,
    string ServiceAccountJson);
