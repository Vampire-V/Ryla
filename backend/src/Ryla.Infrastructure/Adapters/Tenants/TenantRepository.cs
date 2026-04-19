using Ryla.Core.Domain;
using Ryla.Core.Ports.Outbound;
using Ryla.Infrastructure.Adapters.Database;

namespace Ryla.Infrastructure.Adapters.Tenants;

/// <summary>
/// Npgsql implementation ของ ITenantRepository
/// อ่านข้อมูล tenant/profile จาก PostgreSQL
/// Write operations ทำผ่าน Supabase Auth trigger
/// </summary>
internal sealed class TenantRepository : ITenantRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public TenantRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<Tenant?> GetByIdAsync(
        Guid tenantId, CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.CreateAsync(cancellationToken);
        await using var cmd = connection.CreateCommand();

        cmd.CommandText = "SELECT id, name, created_at FROM tenants WHERE id = @id";
        cmd.Parameters.AddWithValue("id", tenantId);

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            return null;

        return new Tenant(
            reader.GetGuid(0),
            reader.GetString(1),
            reader.GetFieldValue<DateTimeOffset>(2));
    }

    public async Task<Tenant?> GetByUserIdAsync(
        Guid userId, CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.CreateAsync(cancellationToken);
        await using var cmd = connection.CreateCommand();

        cmd.CommandText = """
            SELECT t.id, t.name, t.created_at
            FROM tenants t
            INNER JOIN profiles p ON p.tenant_id = t.id
            WHERE p.id = @userId
            """;
        cmd.Parameters.AddWithValue("userId", userId);

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            return null;

        return new Tenant(
            reader.GetGuid(0),
            reader.GetString(1),
            reader.GetFieldValue<DateTimeOffset>(2));
    }

    public async Task<UserProfile?> GetProfileByUserIdAsync(
        Guid userId, CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.CreateAsync(cancellationToken);
        await using var cmd = connection.CreateCommand();

        cmd.CommandText = "SELECT id, tenant_id, full_name, created_at FROM profiles WHERE id = @userId";
        cmd.Parameters.AddWithValue("userId", userId);

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            return null;

        return new UserProfile(
            reader.GetGuid(0),
            reader.GetGuid(1),
            reader.IsDBNull(2) ? null : reader.GetString(2),
            reader.GetFieldValue<DateTimeOffset>(3));
    }
}
