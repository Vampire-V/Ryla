using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Ryla.Api.Endpoints;
using Ryla.Core.Configuration;
using Ryla.Core.Domain.Orders;
using Ryla.Core.Domain.Webhooks;
using Ryla.Core.Ports.Outbound;
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
[JsonSerializable(typeof(TestNotificationRequest))]
[JsonSerializable(typeof(TestNotificationResponse))]
[JsonSerializable(typeof(SimulateOrderRequest))]
[JsonSerializable(typeof(SimulateOrderResponse))]
// ─── Profit Dashboard types ───────────────────────────────────────────────────
[JsonSerializable(typeof(ProfitSummary))]
[JsonSerializable(typeof(ShopeeOrder))]
[JsonSerializable(typeof(ShopeeOrder[]))]
[JsonSerializable(typeof(OrdersResponse))]
[JsonSerializable(typeof(SkuCost))]
[JsonSerializable(typeof(SkuCost[]))]
[JsonSerializable(typeof(SkuCostsResponse))]
[JsonSerializable(typeof(SyncProgress))]
[JsonSerializable(typeof(UpsertSkuCostRequest))]
[JsonSerializable(typeof(AuthorizeResponse))]
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

        // ─── JWT Bearer Authentication (Supabase HMAC-SHA256) ─────────────────
        // ใช้ explicit TokenValidationParameters (ไม่ใช้ Authority/MetadataAddress)
        // เพื่อหลีกเลี่ยง OIDC discovery ที่มี reflection — AOT-safe
        AddSupabaseJwtAuth(services, configuration);

        // Authorization policy ─────────────────────────────────────────────────
        services.AddAuthorization();

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

    private static void AddSupabaseJwtAuth(
        IServiceCollection services,
        IConfiguration configuration)
    {
        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                // ห้าม Authority/MetadataAddress — มี IL2026 จาก OIDC discovery
                options.MapInboundClaims = false;
                // TokenValidationParameters ถูก configure ใน IPostConfigureOptions<JwtBearerOptions>
                // เพื่อให้ IConfiguration อ่านค่าได้ถูกต้องหลัง WebApplicationFactory override
            });

        // ใช้ IPostConfigureOptions เพื่อ configure JwtBearerOptions หลัง DI resolution
        // ทำให้ WebApplicationFactory test overrides ทำงานได้ถูกต้อง
        services.AddSingleton<IPostConfigureOptions<JwtBearerOptions>,
            SupabaseJwtPostConfigureOptions>();
    }

    private sealed class SupabaseJwtPostConfigureOptions(IConfiguration configuration)
        : IPostConfigureOptions<JwtBearerOptions>
    {
        public void PostConfigure(string? name, JwtBearerOptions options)
        {
            if (name != JwtBearerDefaults.AuthenticationScheme) return;

            var jwtSecret = configuration["Supabase:JwtSecret"];
            if (string.IsNullOrEmpty(jwtSecret))
                throw new InvalidOperationException(
                    "Supabase:JwtSecret is not configured or empty. " +
                    "Set it in appsettings.Development.json or via environment variable.");

            var supabaseUrl = configuration["Supabase:Url"] ?? "";
            var issuer = string.IsNullOrEmpty(supabaseUrl)
                ? null
                : $"{supabaseUrl.TrimEnd('/')}/auth/v1";

            var keyBytes = Encoding.UTF8.GetBytes(jwtSecret);

            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateAudience = true,
                ValidAudience = "authenticated",
                ValidateIssuer = issuer is not null,
                ValidIssuer = issuer,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.FromSeconds(30),
                NameClaimType = "sub",
                // SignatureValidator ที่ validate HMAC-SHA256 โดยตรง
                // หลีกเลี่ยงปัญหา kid-based key resolution ใน JsonWebTokenHandler v8
                // Supabase JWT ไม่มี kid header ทำให้ standard key resolution fail
                SignatureValidator = (token, _) => ValidateHmacSignature(token, keyBytes),
            };
        }
    }

    /// <summary>
    /// Validate HMAC-SHA256 signature ของ JWT โดยตรง
    /// คำนวณ expected_sig = HMAC-SHA256(key, header.payload) แล้วเปรียบเทียบกับ signature ใน token
    /// </summary>
    private static JsonWebToken ValidateHmacSignature(string token, byte[] keyBytes)
    {
        var parts = token.Split('.');
        if (parts.Length != 3)
            throw new SecurityTokenException("Invalid JWT format");

        var headerPayload = Encoding.UTF8.GetBytes($"{parts[0]}.{parts[1]}");
        var expectedSig = HMACSHA256.HashData(keyBytes, headerPayload);
        var actualSig = Base64UrlEncoder.DecodeBytes(parts[2]);

        // Constant-time comparison เพื่อป้องกัน timing attack
        if (!CryptographicOperations.FixedTimeEquals(expectedSig, actualSig))
            throw new SecurityTokenInvalidSignatureException("JWT signature validation failed");

        return new JsonWebToken(token);
    }
}
