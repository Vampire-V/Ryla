using Ryla.Core.Domain.Orders;
using Ryla.Core.Ports.Outbound;
using Ryla.Infrastructure.Adapters.Database;

namespace Ryla.Infrastructure.Adapters.Shopee;

/// <summary>
/// Shopee OAuth token storage ผ่าน Supabase Vault (pgsodium)
/// เก็บ secret_id (UUID) ใน shopee_tokens — ไม่เก็บ plaintext token ใน table
/// </summary>
internal sealed class ShopeeTokenAdapter(IDbConnectionFactory connectionFactory) : IShopeeTokenPort
{
    public async Task<ShopeeTokenSecrets?> GetTokenAsync(Guid tenantId, CancellationToken ct = default)
    {
        await using var conn = await connectionFactory.CreateAsync(ct);
        await using var cmd = conn.CreateCommand();

        // JOIN shopee_tokens กับ vault.decrypted_secrets 2 ครั้ง (access + refresh)
        cmd.CommandText = """
            SELECT
              st.shop_id,
              st.expires_at,
              a.decrypted_secret AS access_token,
              r.decrypted_secret AS refresh_token
            FROM shopee_tokens st
            JOIN vault.decrypted_secrets a ON a.id = st.access_token_secret_id
            JOIN vault.decrypted_secrets r ON r.id = st.refresh_token_secret_id
            WHERE st.tenant_id = @tenantId
            """;
        cmd.Parameters.AddWithValue("tenantId", tenantId);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
            return null;

        return new ShopeeTokenSecrets(
            TenantId: tenantId,
            ShopId: reader.GetInt64(0),
            AccessToken: reader.GetString(2),
            RefreshToken: reader.GetString(3),
            ExpiresAt: reader.GetFieldValue<DateTimeOffset>(1));
    }

    public async Task SaveTokenAsync(
        Guid tenantId, long shopId, string accessToken, string refreshToken,
        DateTimeOffset expiresAt, CancellationToken ct = default)
    {
        await using var conn = await connectionFactory.CreateAsync(ct);
        await using var transaction = await conn.BeginTransactionAsync(ct);
        try
        {
            // สร้าง vault secrets สำหรับ access + refresh tokens
            var accessSecretId = await CreateVaultSecretAsync(conn, transaction, accessToken, ct);
            var refreshSecretId = await CreateVaultSecretAsync(conn, transaction, refreshToken, ct);

            await using var upsertCmd = conn.CreateCommand();
            upsertCmd.Transaction = transaction;
            upsertCmd.CommandText = """
                INSERT INTO shopee_tokens
                  (tenant_id, shop_id, access_token_secret_id, refresh_token_secret_id, expires_at)
                VALUES (@tenantId, @shopId, @accessSecretId, @refreshSecretId, @expiresAt)
                ON CONFLICT (tenant_id) DO UPDATE SET
                  shop_id                  = EXCLUDED.shop_id,
                  access_token_secret_id   = EXCLUDED.access_token_secret_id,
                  refresh_token_secret_id  = EXCLUDED.refresh_token_secret_id,
                  expires_at               = EXCLUDED.expires_at,
                  updated_at               = now()
                """;
            upsertCmd.Parameters.AddWithValue("tenantId", tenantId);
            upsertCmd.Parameters.AddWithValue("shopId", shopId);
            upsertCmd.Parameters.AddWithValue("accessSecretId", accessSecretId);
            upsertCmd.Parameters.AddWithValue("refreshSecretId", refreshSecretId);
            upsertCmd.Parameters.AddWithValue("expiresAt", expiresAt);
            await upsertCmd.ExecuteNonQueryAsync(ct);

            await transaction.CommitAsync(ct);
        }
        catch
        {
            await transaction.RollbackAsync(ct);
            throw;
        }
    }

    public async Task UpdateAccessTokenAsync(
        Guid tenantId, string newAccessToken, DateTimeOffset newExpiresAt,
        CancellationToken ct = default)
    {
        await using var conn = await connectionFactory.CreateAsync(ct);
        await using var transaction = await conn.BeginTransactionAsync(ct);
        try
        {
            // ดึง secret_id เดิมก่อน แล้ว update ใน Vault
            await using var getCmd = conn.CreateCommand();
            getCmd.Transaction = transaction;
            getCmd.CommandText = "SELECT access_token_secret_id FROM shopee_tokens WHERE tenant_id = @tenantId";
            getCmd.Parameters.AddWithValue("tenantId", tenantId);
            var secretId = (Guid)(await getCmd.ExecuteScalarAsync(ct))!;

            // vault.update_secret(secret_id, new_value)
            await using var vaultCmd = conn.CreateCommand();
            vaultCmd.Transaction = transaction;
            vaultCmd.CommandText = "SELECT vault.update_secret(@secretId, @newToken)";
            vaultCmd.Parameters.AddWithValue("secretId", secretId);
            vaultCmd.Parameters.AddWithValue("newToken", newAccessToken);
            await vaultCmd.ExecuteNonQueryAsync(ct);

            await using var updateCmd = conn.CreateCommand();
            updateCmd.Transaction = transaction;
            updateCmd.CommandText = """
                UPDATE shopee_tokens SET
                  expires_at = @expiresAt,
                  updated_at = now()
                WHERE tenant_id = @tenantId
                """;
            updateCmd.Parameters.AddWithValue("tenantId", tenantId);
            updateCmd.Parameters.AddWithValue("expiresAt", newExpiresAt);
            await updateCmd.ExecuteNonQueryAsync(ct);

            await transaction.CommitAsync(ct);
        }
        catch
        {
            await transaction.RollbackAsync(ct);
            throw;
        }
    }

    public async Task<IReadOnlyList<Guid>> GetTenantIdsWithTokenAsync(CancellationToken ct = default)
    {
        await using var conn = await connectionFactory.CreateAsync(ct);
        await using var cmd = conn.CreateCommand();

        cmd.CommandText = "SELECT tenant_id FROM shopee_tokens ORDER BY updated_at DESC";

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        var results = new List<Guid>();
        while (await reader.ReadAsync(ct))
            results.Add(reader.GetGuid(0));

        return results;
    }

    public async Task<Guid?> GetTenantIdByShopIdAsync(long shopId, CancellationToken ct = default)
    {
        await using var conn = await connectionFactory.CreateAsync(ct);
        await using var cmd = conn.CreateCommand();

        cmd.CommandText = "SELECT tenant_id FROM shopee_tokens WHERE shop_id = @shopId LIMIT 1";
        cmd.Parameters.AddWithValue("shopId", shopId);

        var result = await cmd.ExecuteScalarAsync(ct);
        return result is Guid tenantId ? tenantId : null;
    }

    private static async Task<Guid> CreateVaultSecretAsync(
        Npgsql.NpgsqlConnection conn,
        Npgsql.NpgsqlTransaction transaction,
        string secret,
        CancellationToken ct)
    {
        await using var cmd = conn.CreateCommand();
        cmd.Transaction = transaction;
        cmd.CommandText = "SELECT vault.create_secret(@secret)";
        cmd.Parameters.AddWithValue("secret", secret);
        return (Guid)(await cmd.ExecuteScalarAsync(ct))!;
    }
}
