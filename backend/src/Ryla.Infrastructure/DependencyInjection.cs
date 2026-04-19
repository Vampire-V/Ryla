using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Ryla.Core.Ports.Outbound;
using Ryla.Infrastructure.Adapters.Database;
using Ryla.Infrastructure.Adapters.Tenants;

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
        // Tenant repository: Scoped เพราะแต่ละ request ควรได้ connection ใหม่
        services.AddScoped<ITenantRepository, TenantRepository>();

        return services;
    }
}
