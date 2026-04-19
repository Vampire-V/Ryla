namespace Ryla.Core.Domain.Integrations;

/// <summary>
/// LINE OA credentials ที่ดึงมาจาก connections table
/// </summary>
public sealed record LineCredentials(string ChannelAccessToken, string TargetUserId);
