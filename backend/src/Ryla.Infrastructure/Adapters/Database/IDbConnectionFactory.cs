using Npgsql;

namespace Ryla.Infrastructure.Adapters.Database;

/// <summary>
/// Factory สำหรับสร้าง database connections
/// เป็น Infrastructure concern — ไม่ leak เข้าไปใน Core
/// </summary>
public interface IDbConnectionFactory
{
    /// <summary>สร้าง open connection ไปยัง Supabase PostgreSQL</summary>
    ValueTask<NpgsqlConnection> CreateAsync(CancellationToken cancellationToken = default);
}
