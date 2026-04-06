using Npgsql;

namespace Ryla.Infrastructure.Adapters.Database;

/// <summary>
/// Npgsql implementation ของ IDbConnectionFactory
/// ใช้ NpgsqlDataSource ซึ่ง AOT-compatible ตั้งแต่ Npgsql v8+
/// </summary>
internal sealed class NpgsqlConnectionFactory : IDbConnectionFactory, IAsyncDisposable
{
    private readonly NpgsqlDataSource _dataSource;

    public NpgsqlConnectionFactory(string connectionString)
    {
        // NpgsqlDataSourceBuilder: AOT-safe, reuses connections ผ่าน connection pool
        var builder = new NpgsqlDataSourceBuilder(connectionString);
        _dataSource = builder.Build();
    }

    /// <inheritdoc />
    public async ValueTask<NpgsqlConnection> CreateAsync(
        CancellationToken cancellationToken = default)
    {
        // OpenConnectionAsync: คืน connection ที่พร้อมใช้งานทันที
        return await _dataSource.OpenConnectionAsync(cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await _dataSource.DisposeAsync();
    }
}
