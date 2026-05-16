using Microsoft.Extensions.DependencyInjection;
using Ryla.Core.Ports.Outbound;
using Ryla.Core.UseCases.ProfitDashboard;
using Ryla.Core.UseCases.ShopeeOAuth;
using Ryla.Core.UseCases.SkuCosts;
using Ryla.Core.UseCases.StoreOrderFromWebhook;
using Ryla.Core.UseCases.SyncOrders;
using Ryla.Core.UseCases.TenantResolution;

namespace Ryla.Core.UseCases;

/// <summary>
/// DI registration extension สำหรับ Core use cases
/// เก็บไว้ใน Core เพื่อให้ internal classes ถูก register ได้โดยไม่ต้อง expose เป็น public
/// </summary>
public static class CoreServiceCollectionExtensions
{
    public static IServiceCollection AddRylaCoreUseCases(this IServiceCollection services)
    {
        // Existing
        services.AddScoped<IProcessOrderWebhookUseCase, ProcessOrderWebhookUseCase>();
        services.AddScoped<ITestNotificationUseCase, TestNotificationUseCase>();
        services.AddScoped<ISimulateOrderUseCase, SimulateOrderUseCase>();

        // Webhook order storage
        services.AddScoped<IStoreOrderFromWebhookUseCase, StoreOrderFromWebhookUseCase>();

        // Profit dashboard queries
        services.AddScoped<IGetProfitSummaryUseCase, GetProfitSummaryUseCase>();
        services.AddScoped<IGetOrdersUseCase, GetOrdersUseCase>();
        services.AddScoped<IGetSyncStatusUseCase, GetSyncStatusUseCase>();

        // COGS management
        services.AddScoped<IUpsertSkuCostUseCase, UpsertSkuCostUseCase>();
        services.AddScoped<IGetSkuCostsUseCase, GetSkuCostsUseCase>();

        // Shopee OAuth + sync
        services.AddScoped<IAuthorizeShopeeUseCase, AuthorizeShopeeUseCase>();
        services.AddScoped<IHandleShopeeCallbackUseCase, HandleShopeeCallbackUseCase>();
        services.AddScoped<IRefreshShopeeTokenUseCase, RefreshShopeeTokenUseCase>();
        services.AddScoped<ISyncOrdersUseCase, SyncOrdersUseCase>();

        // Tenant resolution — แก้ IDOR: resolve tenant จาก JWT sub ไม่ใช่ header
        services.AddScoped<ITenantResolver, TenantResolverService>();

        return services;
    }
}
