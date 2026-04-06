using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Ryla.Infrastructure.Adapters.Database;

namespace Ryla.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddRylaInfrastructureServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // ─── Database ───────────────────────────────────────────────────────────
        var connectionString = configuration.GetConnectionString("Supabase")
            ?? throw new InvalidOperationException(
                "ConnectionStrings:Supabase is not configured. " +
                "Set it in appsettings.Development.json or via dotnet user-secrets.");

        // Singleton: NpgsqlDataSource manages the connection pool — สร้างครั้งเดียวต่อ app lifetime
        services.AddSingleton<IDbConnectionFactory>(
            new NpgsqlConnectionFactory(connectionString));

        // ─── Adapters ────────────────────────────────────────────────────────────
        // TODO: ลงทะเบียน adapters ที่นี่เมื่อสร้าง (explicit, ห้ามใช้ assembly scanning)
        // ตัวอย่าง: services.AddScoped<INotificationPort, LineOaAdapter>();
        // ตัวอย่าง: services.AddScoped<ITenantRepository, NpgsqlTenantRepository>();

        return services;
    }
}
