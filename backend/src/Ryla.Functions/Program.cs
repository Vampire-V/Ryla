using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Ryla.Core.Configuration;
using Ryla.Core.UseCases;
using Ryla.Infrastructure;

// ConfigureFunctionsWorkerDefaults จัดการ configuration pipeline ให้เอง:
// - local.settings.json Values (local dev, ถูก inject เป็น env vars โดย func CLI)
// - Environment variables (Azure App Settings / Key Vault)
var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureServices((ctx, services) =>
    {
        services.Configure<ShopeeOptions>(ctx.Configuration.GetSection(ShopeeOptions.SectionName));
        services.Configure<LineOptions>(ctx.Configuration.GetSection(LineOptions.SectionName));
        services.Configure<GoogleSheetsOptions>(ctx.Configuration.GetSection(GoogleSheetsOptions.SectionName));

        services.AddRylaCoreUseCases();
        services.AddRylaInfrastructureServices(ctx.Configuration);
    })
    .Build();

await host.RunAsync();
