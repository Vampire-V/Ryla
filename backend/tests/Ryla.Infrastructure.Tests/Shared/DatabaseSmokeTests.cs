using Npgsql;
using Ryla.Infrastructure.Adapters.Database;

namespace Ryla.Infrastructure.Tests.Shared;

/// <summary>
/// Smoke tests ตรวจสอบว่า Testcontainers + schema setup ทำงานได้ถูกต้อง
/// </summary>
[Collection(nameof(PostgresCollection))]
public sealed class DatabaseSmokeTests
{
    private readonly PostgresFixture _fixture;

    public DatabaseSmokeTests(PostgresFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Database__WhenContainerStarts__ShouldBeReachable()
    {
        await using var connection = new NpgsqlConnection(_fixture.ConnectionString);
        await connection.OpenAsync();

        Assert.Equal(System.Data.ConnectionState.Open, connection.State);
    }

    [Fact]
    public async Task Database__AfterMigrationsApplied__ShouldHaveTenantsTable()
    {
        await using var connection = new NpgsqlConnection(_fixture.ConnectionString);
        await connection.OpenAsync();

        await using var cmd = new NpgsqlCommand(
            "SELECT COUNT(*) FROM information_schema.tables WHERE table_name = 'tenants'",
            connection);

        var count = (long)(await cmd.ExecuteScalarAsync() ?? 0L);
        Assert.Equal(1L, count);
    }

    [Fact]
    public async Task Database__AfterMigrationsApplied__ShouldHaveRlsOnAllTables()
    {
        await using var connection = new NpgsqlConnection(_fixture.ConnectionString);
        await connection.OpenAsync();

        await using var cmd = new NpgsqlCommand("""
            SELECT COUNT(*) FROM pg_tables
            WHERE schemaname = 'public'
              AND rowsecurity = false
            """, connection);

        var tablesWithoutRls = (long)(await cmd.ExecuteScalarAsync() ?? 0L);
        Assert.Equal(0L, tablesWithoutRls); // ทุก table ต้องมี RLS
    }

    [Fact]
    public async Task DbConnectionFactory__WhenUsed__ShouldOpenConnection()
    {
        var factory = new NpgsqlConnectionFactory(_fixture.ConnectionString);

        await using var connection = await factory.CreateAsync();

        Assert.Equal(System.Data.ConnectionState.Open, connection.State);
        await factory.DisposeAsync();
    }
}
