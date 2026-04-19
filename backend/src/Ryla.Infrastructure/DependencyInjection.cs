using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Ryla.Core.Ports.Outbound;
using Ryla.Infrastructure.Adapters.Connections;
using Ryla.Infrastructure.Adapters.Database;
using Ryla.Infrastructure.Adapters.LineMessaging;
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

        // Singleton: NpgsqlDataSource manages the connection pool
        services.AddSingleton<IDbConnectionFactory>(
            new NpgsqlConnectionFactory(connectionString));

        // ─── Adapters ────────────────────────────────────────────────────────────
        services.AddScoped<ITenantRepository, TenantRepository>();
        services.AddScoped<IConnectionRepository, ConnectionRepository>();

        // ─── LINE Messaging API ──────────────────────────────────────────────────
        // Note: LineOptions configuration happens in Api layer (ServiceCollectionExtensions)
        // เพื่อหลีกเลี่ยง IL2026/IL3050 warnings ใน AOT-compatible library
        services.AddHttpClient<ILineNotifier, LineMessagingClient>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(10);
        });

        return services;
    }
}
