using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Ryla.Api.Endpoints;
using Ryla.Core.Configuration;
using Ryla.Core.Domain.Webhooks;
using Ryla.Core.Services;
using Ryla.Core.UseCases;
using Ryla.Infrastructure.Adapters.LineMessaging;
using Ryla.Infrastructure.Adapters.GoogleSheets;

namespace Ryla.Api.Extensions;

// Source-generated JSON context — วิธีเดียวที่ AOT-safe สำหรับ serialization
// ทุก type ที่ผ่าน HTTP boundary ต้องลงทะเบียนที่นี่
[JsonSerializable(typeof(HealthResponse))]
[JsonSerializable(typeof(TikTokWebhookPayload))]
[JsonSerializable(typeof(TikTokOrderContent))]
[JsonSerializable(typeof(ShopeeWebhookPayload))]
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

        services.Configure<ShopeeOptions>(
            configuration.GetSection(ShopeeOptions.SectionName));

        services.AddScoped<IShopeeHmacVerifier, ShopeeHmacVerifier>();

        // ─── Core use cases ───────────────────────────────────────────────────
        services.AddRylaCoreUseCases();

        // ─── LINE Options + JSON context ──────────────────────────────────────
        services.Configure<LineOptions>(configuration.GetSection("Line"));

        services.ConfigureHttpJsonOptions(opts =>
            opts.SerializerOptions.TypeInfoResolverChain.Insert(0, LineJsonContext.Default));

        // ─── Google Sheets Options + JSON context ─────────────────────────────
        services.Configure<GoogleSheetsOptions>(
            configuration.GetSection(GoogleSheetsOptions.SectionName));

        services.ConfigureHttpJsonOptions(opts =>
            opts.SerializerOptions.TypeInfoResolverChain.Insert(0, GoogleSheetsJsonContext.Default));

        return services;
    }
}
