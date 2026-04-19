using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Ryla.Api.Endpoints;
using Ryla.Core.Configuration;
using Ryla.Core.Domain.Webhooks;
using Ryla.Core.Services;

namespace Ryla.Api.Extensions;

// Source-generated JSON context — วิธีเดียวที่ AOT-safe สำหรับ serialization
// ทุก type ที่ผ่าน HTTP boundary ต้องลงทะเบียนที่นี่
[JsonSerializable(typeof(HealthResponse))]
[JsonSerializable(typeof(TikTokWebhookPayload))]
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    WriteIndented = false)]
internal partial class RylaJsonContext : JsonSerializerContext { }

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddRylaCoreServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<TikTokShopOptions>(
            configuration.GetSection(TikTokShopOptions.SectionName));

        services.AddScoped<ITikTokHmacVerifier, TikTokHmacVerifier>();

        // TODO: ลงทะเบียน Core use cases ที่นี่ (explicit, ห้ามใช้ assembly scanning)
        return services;
    }
}
