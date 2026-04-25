using Ryla.Core.Domain.Integrations;
using Ryla.Core.Domain.Webhooks;
using Ryla.Core.Ports.Outbound;
using Ryla.Infrastructure.Adapters.Database;

namespace Ryla.Infrastructure.Adapters.Connections;

/// <summary>
/// Npgsql implementation ของ IConnectionRepository
/// Query connections table ด้วย raw SQL — ไม่ใช้ reflection-based ORM
/// </summary>
internal sealed class ConnectionRepository(IDbConnectionFactory connectionFactory) : IConnectionRepository
{
    public async Task<Guid?> FindTenantIdByShopIdAsync(
        Platform platform,
        string shopId,
        CancellationToken ct = default)
    {
        await using var conn = await connectionFactory.CreateAsync(ct);
        await using var cmd = conn.CreateCommand();

        cmd.CommandText = """
            SELECT tenant_id FROM connections
            WHERE platform = @platform
              AND is_active = true
              AND credentials->>'shop_id' = @shopId
            LIMIT 1
            """;
        cmd.Parameters.AddWithValue("platform", MapPlatform(platform));
        cmd.Parameters.AddWithValue("shopId", shopId);

        var result = await cmd.ExecuteScalarAsync(ct);
        return result is Guid g ? g : null;
    }

    public async Task<LineCredentials?> GetLineCredentialsAsync(
        Guid tenantId,
        CancellationToken ct = default)
    {
        await using var conn = await connectionFactory.CreateAsync(ct);
        await using var cmd = conn.CreateCommand();

        cmd.CommandText = """
            SELECT credentials->>'channel_access_token', credentials->>'target_user_id'
            FROM connections
            WHERE tenant_id = @tenantId
              AND platform = 'line_oa'
              AND is_active = true
            LIMIT 1
            """;
        cmd.Parameters.AddWithValue("tenantId", tenantId);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
            return null;

        var token = reader.IsDBNull(0) ? null : reader.GetString(0);
        var userId = reader.IsDBNull(1) ? null : reader.GetString(1);

        if (string.IsNullOrEmpty(token) || string.IsNullOrEmpty(userId))
            return null;

        return new LineCredentials(token, userId);
    }


    public async Task<GoogleSheetsCredentials?> GetGoogleSheetsCredentialsAsync(
        Guid tenantId,
        CancellationToken ct = default)
    {
        await using var conn = await connectionFactory.CreateAsync(ct);
        await using var cmd = conn.CreateCommand();

        cmd.CommandText = """
            SELECT credentials->>'spreadsheet_id',
                   credentials->>'worksheet_name',
                   credentials->>'service_account_json'
            FROM connections
            WHERE tenant_id = @tenantId
              AND platform = 'google_sheets'
              AND is_active = true
            LIMIT 1
            """;
        cmd.Parameters.AddWithValue("tenantId", tenantId);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
            return null;

        var spreadsheetId = reader.IsDBNull(0) ? null : reader.GetString(0);
        var worksheetName = reader.IsDBNull(1) ? null : reader.GetString(1);
        var serviceAccountJson = reader.IsDBNull(2) ? null : reader.GetString(2);

        if (string.IsNullOrEmpty(spreadsheetId)
            || string.IsNullOrEmpty(worksheetName)
            || string.IsNullOrEmpty(serviceAccountJson))
            return null;

        return new GoogleSheetsCredentials(spreadsheetId, worksheetName, serviceAccountJson);
    }

    /// <summary>
    /// Map Platform enum ไปเป็น SQL string ที่ใช้ใน connections.platform column
    /// </summary>
    private static string MapPlatform(Platform platform) => platform switch
    {
        Platform.TikTokShop => "tiktok_shop",
        Platform.Shopee => "shopee",
        _ => throw new ArgumentOutOfRangeException(nameof(platform), platform, null)
    };
}
