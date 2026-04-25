using Microsoft.Extensions.DependencyInjection;

namespace Ryla.Core.UseCases;

/// <summary>
/// DI registration extension สำหรับ Core use cases
/// เก็บไว้ใน Core เพื่อให้ internal classes ถูก register ได้โดยไม่ต้อง expose เป็น public
/// </summary>
public static class CoreServiceCollectionExtensions
{
    public static IServiceCollection AddRylaCoreUseCases(this IServiceCollection services)
    {
        services.AddScoped<IProcessOrderWebhookUseCase, ProcessOrderWebhookUseCase>();
        services.AddScoped<ITestNotificationUseCase, TestNotificationUseCase>();
        return services;
    }
}
