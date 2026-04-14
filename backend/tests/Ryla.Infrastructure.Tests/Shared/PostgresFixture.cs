using DotNet.Testcontainers.Builders;
using Npgsql;
using Testcontainers.PostgreSql;

namespace Ryla.Infrastructure.Tests.Shared;

/// <summary>
/// xUnit Collection Fixture: รัน PostgreSQL container หนึ่งครั้งต่อ test collection
/// ทุก integration test ที่ต้องการ DB ให้ใช้ fixture นี้ผ่าน [Collection(nameof(PostgresCollection))]
/// </summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container;

    public string ConnectionString { get; private set; } = string.Empty;

    public PostgresFixture()
    {
        _container = new PostgreSqlBuilder()
            .WithImage("postgres:15-alpine")
            .WithDatabase("ryla_test")
            .WithUsername("postgres")
            .WithPassword("postgres")
            .WithWaitStrategy(Wait.ForUnixContainer().UntilPortIsAvailable(5432))
            .Build();
    }

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        ConnectionString = _container.GetConnectionString();
        await ApplySchemaAsync();
    }

    public Task DisposeAsync() => _container.StopAsync();

    /// <summary>
    /// Apply supabase migrations ตามลำดับ + mock auth schema สำหรับ test
    /// </summary>
    private async Task ApplySchemaAsync()
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();

        // Mock Supabase auth schema ก่อน — migrations อ้างอิง auth.users
        await ExecuteSqlAsync(connection, """
            CREATE SCHEMA IF NOT EXISTS auth;
            CREATE TABLE IF NOT EXISTS auth.users (
                id   UUID PRIMARY KEY DEFAULT gen_random_uuid(),
                email TEXT UNIQUE,
                raw_user_meta_data JSONB DEFAULT '{}'::jsonb
            );
            -- ฟังก์ชัน auth.uid() สำหรับ RLS policies
            CREATE OR REPLACE FUNCTION auth.uid() RETURNS UUID
                LANGUAGE sql STABLE AS $$SELECT '00000000-0000-0000-0000-000000000000'::uuid$$;
            """);

        // Apply migrations ตามลำดับ alphabetical (รูปแบบ: YYYYMMDDHHMMSS_*.sql)
        var migrationsDir = Path.Combine(AppContext.BaseDirectory, "migrations");
        if (!Directory.Exists(migrationsDir))
        {
            throw new DirectoryNotFoundException(
                $"Migrations directory not found: {migrationsDir}\n" +
                "ตรวจสอบว่า supabase/migrations/ ถูก copy ไปยัง output directory");
        }

        foreach (var file in Directory.GetFiles(migrationsDir, "*.sql").OrderBy(f => f))
        {
            var sql = await File.ReadAllTextAsync(file);
            await ExecuteSqlAsync(connection, sql);
        }
    }

    private static async Task ExecuteSqlAsync(NpgsqlConnection connection, string sql)
    {
        await using var cmd = new NpgsqlCommand(sql, connection);
        await cmd.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// ล้าง data ระหว่าง tests โดยไม่ drop schema
    /// เหมาะสำหรับ tests ที่ต้องการ clean state แต่ไม่ต้องการ restart container
    /// </summary>
    public async Task ResetDataAsync()
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        await ExecuteSqlAsync(connection, """
            TRUNCATE TABLE connections, profiles, tenants RESTART IDENTITY CASCADE;
            TRUNCATE TABLE auth.users RESTART IDENTITY CASCADE;
            """);
    }
}

/// <summary>
/// xUnit Collection definition — ทุก integration test ที่ต้องการ DB
/// ใช้: [Collection(nameof(PostgresCollection))]
/// </summary>
[CollectionDefinition(nameof(PostgresCollection))]
public sealed class PostgresCollection : ICollectionFixture<PostgresFixture>;
