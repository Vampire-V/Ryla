using Npgsql;

namespace Ryla.Infrastructure.Adapters.Database;

/// <summary>
/// Npgsql implementation ของ IDbConnectionFactory
/// ใช้ NpgsqlDataSource ซึ่ง AOT-compatible ตั้งแต่ Npgsql v8+
/// DataSource ถูก build แบบ lazy — ไม่ throw ตอน DI registration
/// </summary>
internal sealed class NpgsqlConnectionFactory : IDbConnectionFactory, IAsyncDisposable
{
    private readonly Lazy<NpgsqlDataSource> _dataSource;

    public NpgsqlConnectionFactory(string connectionString)
    {
        // Lazy: build เมื่อใช้งานจริง — ทำให้ startup ไม่ throw แม้ว่า connection string
        // ยังไม่สมบูรณ์ (เช่น ตอนรัน unit tests ที่ไม่ต้องการ DB)
        _dataSource = new Lazy<NpgsqlDataSource>(
            () => new NpgsqlDataSourceBuilder(connectionString).Build());
    }

    /// <inheritdoc />
    public async ValueTask<NpgsqlConnection> CreateAsync(
        CancellationToken cancellationToken = default)
    {
        // OpenConnectionAsync: คืน connection ที่พร้อมใช้งานทันที
        return await _dataSource.Value.OpenConnectionAsync(cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        if (_dataSource.IsValueCreated)
            await _dataSource.Value.DisposeAsync();
    }
}
