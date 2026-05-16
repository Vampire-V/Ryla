using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using NSubstitute;
using Ryla.Core.Common;
using Ryla.Core.Domain.Orders;
using Ryla.Core.Ports.Outbound;
using Ryla.Core.UseCases.ProfitDashboard;
using Ryla.Core.UseCases.SkuCosts;
using Ryla.Infrastructure.Adapters.Database;
using Xunit;

namespace Ryla.Api.Tests.Endpoints;

/// <summary>
/// ทดสอบ IDOR protection บน Profit Dashboard endpoints
/// ทุก endpoint ต้องการ JWT ที่ valid และ tenant ที่ resolve ได้จาก sub claim
/// </summary>
public class ProfitEndpointsTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    // ค่า fixed สำหรับ test JWT signing — ต้องมีความยาว >= 32 chars สำหรับ HMAC-SHA256
    private const string TestJwtSecret = "ryla-test-jwt-secret-32-chars-min!";

    private static readonly Guid TestUserId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid TestTenantId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

    public ProfitEndpointsTests(WebApplicationFactory<Program> factory) =>
        _factory = factory;

    // ─── JWT helpers ─────────────────────────────────────────────────────────

    private static string CreateValidJwt(Guid userId, string secret = TestJwtSecret)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: "https://test.supabase.co/auth/v1",
            audience: "authenticated",
            claims: [new Claim("sub", userId.ToString())],
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static string CreateExpiredJwt(Guid userId)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(TestJwtSecret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: "https://test.supabase.co/auth/v1",
            audience: "authenticated",
            claims: [new Claim("sub", userId.ToString())],
            notBefore: DateTime.UtcNow.AddHours(-2),
            expires: DateTime.UtcNow.AddHours(-1),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    // ─── Client factory ──────────────────────────────────────────────────────

    private HttpClient CreateTestClient(
        ITenantResolver? tenantResolver = null,
        IGetProfitSummaryUseCase? summaryUseCase = null)
    {
        var resolver = tenantResolver ?? Substitute.For<ITenantResolver>();
        var summary = summaryUseCase ?? Substitute.For<IGetProfitSummaryUseCase>();
        var ordersUseCase = Substitute.For<IGetOrdersUseCase>();
        var skuCostsUseCase = Substitute.For<IGetSkuCostsUseCase>();
        var upsertUseCase = Substitute.For<IUpsertSkuCostUseCase>();
        var syncStatusUseCase = Substitute.For<IGetSyncStatusUseCase>();

        return _factory.WithWebHostBuilder(builder =>
        {
            // ตั้ง environment เป็น Testing เพื่อป้องกัน appsettings.Development.json override config
            builder.UseEnvironment("Testing");

            builder.ConfigureAppConfiguration((_, config) =>
            {
                // Environment variables override appsettings.json — highest priority
                // ใช้ prefix __ สำหรับ nested config (Supabase__JwtSecret = Supabase:JwtSecret)
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Supabase:JwtSecret"] = TestJwtSecret,
                    ["Supabase:Url"] = "https://test.supabase.co",
                    ["ConnectionStrings:Supabase"] = "Host=localhost;Port=54322;Database=postgres;Username=postgres;Password=postgres",
                    ["TikTokShop:ClientSecret"] = "test",
                    ["Shopee:PartnerKey"] = "test",
                    ["Shopee:CallbackUrl"] = "http://localhost/webhooks/shopee",
                    ["Line:BaseUrl"] = "https://api.line.me",
                    ["GoogleSheets:BaseUrl"] = "https://sheets.googleapis.com",
                    ["GoogleSheets:TokenEndpoint"] = "https://oauth2.googleapis.com/token",
                });
                // เพิ่ม Environment Variables source หลัง InMemoryCollection
                // เพื่อให้ InMemoryCollection values ถูก override ได้โดย real env vars ในระบบ
                config.AddEnvironmentVariables();
            });

            builder.ConfigureTestServices(services =>
            {
                // ปิด DbStartupProbe
                foreach (var d in services
                    .Where(s => s.ImplementationType == typeof(DbStartupProbe))
                    .ToList())
                    services.Remove(d);

                // แทนที่ use cases ด้วย mock
                ReplaceService<IGetProfitSummaryUseCase>(services, summary);
                ReplaceService<IGetOrdersUseCase>(services, ordersUseCase);
                ReplaceService<IGetSkuCostsUseCase>(services, skuCostsUseCase);
                ReplaceService<IUpsertSkuCostUseCase>(services, upsertUseCase);
                ReplaceService<IGetSyncStatusUseCase>(services, syncStatusUseCase);
                ReplaceService<ITenantResolver>(services, resolver);
            });
        }).CreateClient();
    }

    private static void ReplaceService<T>(IServiceCollection services, T implementation)
        where T : class
    {
        var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(T));
        if (descriptor is not null) services.Remove(descriptor);
        services.AddScoped<T>(_ => implementation);
    }

    // ─── Tests: Auth protection ───────────────────────────────────────────────

    [Fact]
    public async Task GetSummary__WhenNoAuthHeader__ShouldReturn401()
    {
        var client = CreateTestClient();

        var response = await client.GetAsync("/api/profit/summary");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetSummary__WhenExpiredJwt__ShouldReturn401()
    {
        var client = CreateTestClient();
        var jwt = CreateExpiredJwt(TestUserId);
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", jwt);

        var response = await client.GetAsync("/api/profit/summary");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetSummary__WhenJwtSignedWithWrongSecret__ShouldReturn401()
    {
        var client = CreateTestClient();
        var jwt = CreateValidJwt(TestUserId, secret: "wrong-secret-that-is-definitely-32chars!!");
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", jwt);

        var response = await client.GetAsync("/api/profit/summary");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetSummary__WhenXTenantIdHeaderOnly__ShouldReturn401()
    {
        // ทดสอบว่า X-Tenant-Id header ไม่ bypass auth อีกต่อไป
        var client = CreateTestClient();
        client.DefaultRequestHeaders.Add("X-Tenant-Id", TestTenantId.ToString());

        var response = await client.GetAsync("/api/profit/summary");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ─── Tests: IDOR prevention ───────────────────────────────────────────────

    [Fact]
    public async Task GetSummary__WhenValidJwtButNoProfile__ShouldReturn403()
    {
        var resolver = Substitute.For<ITenantResolver>();
        resolver.ResolveAsync(TestUserId, Arg.Any<CancellationToken>())
            .Returns(Result<Guid>.Fail(new Error("NO_PROFILE", "Profile not found")));

        var client = CreateTestClient(tenantResolver: resolver);
        var jwt = CreateValidJwt(TestUserId);
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", jwt);

        var response = await client.GetAsync("/api/profit/summary");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetSummary__WhenValidJwt__ShouldCallUseCaseWithResolvedTenantId()
    {
        var resolver = Substitute.For<ITenantResolver>();
        resolver.ResolveAsync(TestUserId, Arg.Any<CancellationToken>())
            .Returns(Result<Guid>.Ok(TestTenantId));

        var summaryUseCase = Substitute.For<IGetProfitSummaryUseCase>();
        summaryUseCase
            .ExecuteAsync(
                TestTenantId,
                Arg.Any<DateTimeOffset>(),
                Arg.Any<DateTimeOffset>(),
                Arg.Any<CancellationToken>())
            .Returns(ProfitSummary.Empty());

        var client = CreateTestClient(tenantResolver: resolver, summaryUseCase: summaryUseCase);
        var jwt = CreateValidJwt(TestUserId);
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", jwt);

        var response = await client.GetAsync("/api/profit/summary");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await summaryUseCase.Received(1).ExecuteAsync(
            TestTenantId,
            Arg.Any<DateTimeOffset>(),
            Arg.Any<DateTimeOffset>(),
            Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("/api/profit/summary")]
    [InlineData("/api/profit/orders")]
    [InlineData("/api/profit/sku-costs")]
    [InlineData("/api/profit/sync-status")]
    public async Task GetEndpoints__WhenNoAuth__AllShouldReturn401(string path)
    {
        var client = CreateTestClient();

        var response = await client.GetAsync(path);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
