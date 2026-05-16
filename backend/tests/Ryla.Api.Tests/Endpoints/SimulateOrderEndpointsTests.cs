using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Ryla.Api.Endpoints;
using Ryla.Core.Domain.Webhooks;
using Ryla.Core.UseCases;
using Ryla.Infrastructure.Adapters.Database;
using Xunit;

namespace Ryla.Api.Tests.Endpoints;

public sealed class SimulateOrderEndpointsTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private const string ValidSecret = "test-internal-secret";
    private const string ValidTenantId = "11111111-1111-1111-1111-111111111111";

    public SimulateOrderEndpointsTests(WebApplicationFactory<Program> factory) =>
        _factory = factory;

    private HttpClient CreateClient(ISimulateOrderUseCase? useCase = null)
    {
        var mockUseCase = useCase ?? Substitute.For<ISimulateOrderUseCase>();

        return _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["InternalApiSecret"] = ValidSecret,
                    ["Supabase:JwtSecret"] = "ryla-test-jwt-secret-32-chars-min!",
                    ["ConnectionStrings:Supabase"] = "Host=localhost;Port=54322;Database=postgres;Username=postgres;Password=postgres"
                });
            });

            builder.ConfigureTestServices(services =>
            {
                foreach (var probeDesc in services
                    .Where(d => d.ImplementationType == typeof(DbStartupProbe))
                    .ToList())
                    services.Remove(probeDesc);

                var useCaseDesc = services.SingleOrDefault(
                    d => d.ServiceType == typeof(ISimulateOrderUseCase));
                if (useCaseDesc is not null) services.Remove(useCaseDesc);
                services.AddScoped<ISimulateOrderUseCase>(_ => mockUseCase);
            });
        }).CreateClient();
    }

    [Fact]
    public async Task PostSimulateOrder__WhenSecretIsMissing__ShouldReturn401()
    {
        var client = CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/simulate-order")
        {
            Content = JsonContent.Create(new { tenantId = ValidTenantId, platform = "tiktok_shop" })
        };

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task PostSimulateOrder__WhenSecretIsWrong__ShouldReturn401()
    {
        var client = CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/simulate-order")
        {
            Content = JsonContent.Create(new { tenantId = ValidTenantId, platform = "tiktok_shop" })
        };
        request.Headers.Add("X-Internal-Secret", "wrong-secret");

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task PostSimulateOrder__WhenTenantIdIsInvalid__ShouldReturn400()
    {
        var client = CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/simulate-order")
        {
            Content = JsonContent.Create(new { tenantId = "not-a-guid", platform = "tiktok_shop" })
        };
        request.Headers.Add("X-Internal-Secret", ValidSecret);

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PostSimulateOrder__WhenBothSucceed__ShouldReturn200WithBothTrue()
    {
        var orderId = "SIM-20260510123456";
        var useCase = Substitute.For<ISimulateOrderUseCase>();
        useCase.ExecuteAsync(Arg.Any<Guid>(), Arg.Any<Platform>(), Arg.Any<CancellationToken>())
            .Returns(new SimulateOrderResult(true, null, true, null, orderId));

        var client = CreateClient(useCase);
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/simulate-order")
        {
            Content = JsonContent.Create(new { tenantId = ValidTenantId, platform = "tiktok_shop" })
        };
        request.Headers.Add("X-Internal-Secret", ValidSecret);

        var response = await client.SendAsync(request);
        var body = await response.Content.ReadFromJsonAsync<SimulateOrderResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.True(body.LineSuccess);
        Assert.True(body.SheetsSuccess);
        Assert.Equal(orderId, body.OrderId);
    }

    [Fact]
    public async Task PostSimulateOrder__WhenLineFailsSheetsSoft__ShouldReturn200WithDetails()
    {
        var useCase = Substitute.For<ISimulateOrderUseCase>();
        useCase.ExecuteAsync(Arg.Any<Guid>(), Arg.Any<Platform>(), Arg.Any<CancellationToken>())
            .Returns(new SimulateOrderResult(false, "ยังไม่ได้ตั้งค่า LINE OA", false, "ยังไม่ได้ตั้งค่า Google Sheets", "SIM-x"));

        var client = CreateClient(useCase);
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/simulate-order")
        {
            Content = JsonContent.Create(new { tenantId = ValidTenantId, platform = "tiktok_shop" })
        };
        request.Headers.Add("X-Internal-Secret", ValidSecret);

        var response = await client.SendAsync(request);
        var body = await response.Content.ReadFromJsonAsync<SimulateOrderResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.False(body.LineSuccess);
        Assert.False(body.SheetsSuccess);
        Assert.NotNull(body.LineError);
        Assert.NotNull(body.SheetsError);
    }

    [Fact]
    public async Task PostSimulateOrder__WhenPlatformIsShopee__ShouldCallUseCaseWithShopeePlatform()
    {
        var useCase = Substitute.For<ISimulateOrderUseCase>();
        useCase.ExecuteAsync(Arg.Any<Guid>(), Arg.Any<Platform>(), Arg.Any<CancellationToken>())
            .Returns(new SimulateOrderResult(true, null, false, "ยังไม่ได้ตั้งค่า Google Sheets", "SIM-x"));

        var client = CreateClient(useCase);
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/simulate-order")
        {
            Content = JsonContent.Create(new { tenantId = ValidTenantId, platform = "shopee" })
        };
        request.Headers.Add("X-Internal-Secret", ValidSecret);

        await client.SendAsync(request);

        await useCase.Received(1).ExecuteAsync(
            Arg.Any<Guid>(),
            Platform.Shopee,
            Arg.Any<CancellationToken>());
    }
}
