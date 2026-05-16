using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Ryla.Api.Endpoints;
using Ryla.Core.UseCases;
using Ryla.Infrastructure.Adapters.Database;
using Xunit;

namespace Ryla.Api.Tests.Endpoints;

public sealed class TestNotificationEndpointsTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private const string ValidSecret = "test-internal-secret";
    private const string ValidTenantId = "11111111-1111-1111-1111-111111111111";

    public TestNotificationEndpointsTests(WebApplicationFactory<Program> factory) =>
        _factory = factory;

    private HttpClient CreateClient(ITestNotificationUseCase? useCase = null)
    {
        var mockUseCase = useCase ?? Substitute.For<ITestNotificationUseCase>();

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
                // ปิด DbStartupProbe ใน test (ไม่ต้อง real DB)
                foreach (var probeDesc in services
                    .Where(d => d.ImplementationType == typeof(DbStartupProbe))
                    .ToList())
                    services.Remove(probeDesc);

                var useCaseDesc = services.SingleOrDefault(
                    d => d.ServiceType == typeof(ITestNotificationUseCase));
                if (useCaseDesc is not null) services.Remove(useCaseDesc);
                services.AddScoped<ITestNotificationUseCase>(_ => mockUseCase);
            });
        }).CreateClient();
    }

    [Fact]
    public async Task PostTestNotification__WhenSecretIsMissing__ShouldReturn401()
    {
        var client = CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/test-notification")
        {
            Content = JsonContent.Create(new { tenantId = ValidTenantId })
        };

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task PostTestNotification__WhenSecretIsWrong__ShouldReturn401()
    {
        var client = CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/test-notification")
        {
            Content = JsonContent.Create(new { tenantId = ValidTenantId })
        };
        request.Headers.Add("X-Internal-Secret", "wrong-secret");

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task PostTestNotification__WhenTenantIdIsInvalid__ShouldReturn400()
    {
        var client = CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/test-notification")
        {
            Content = JsonContent.Create(new { tenantId = "not-a-guid" })
        };
        request.Headers.Add("X-Internal-Secret", ValidSecret);

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PostTestNotification__WhenUseCaseSucceeds__ShouldReturn200WithSuccessTrue()
    {
        var useCase = Substitute.For<ITestNotificationUseCase>();
        useCase.ExecuteAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(new TestNotificationResult(true));

        var client = CreateClient(useCase);
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/test-notification")
        {
            Content = JsonContent.Create(new { tenantId = ValidTenantId })
        };
        request.Headers.Add("X-Internal-Secret", ValidSecret);

        var response = await client.SendAsync(request);
        var body = await response.Content.ReadFromJsonAsync<TestNotificationResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.True(body.Success);
        Assert.NotEmpty(body.Message);
    }

    [Fact]
    public async Task PostTestNotification__WhenUseCaseFails__ShouldReturn200WithSuccessFalse()
    {
        var useCase = Substitute.For<ITestNotificationUseCase>();
        useCase.ExecuteAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(new TestNotificationResult(false, "ไม่พบ LINE connection"));

        var client = CreateClient(useCase);
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/test-notification")
        {
            Content = JsonContent.Create(new { tenantId = ValidTenantId })
        };
        request.Headers.Add("X-Internal-Secret", ValidSecret);

        var response = await client.SendAsync(request);
        var body = await response.Content.ReadFromJsonAsync<TestNotificationResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.False(body.Success);
        Assert.NotEmpty(body.Message);
    }
}
