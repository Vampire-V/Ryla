using System.Net;
using System.Text;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Ryla.Core.Configuration;
using Ryla.Core.Domain.Webhooks;
using Ryla.Core.Services;
using Ryla.Core.UseCases;
using Xunit;

namespace Ryla.Api.Tests.Endpoints;

public class TikTokWebhookEndpointsTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public TikTokWebhookEndpointsTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    private HttpClient CreateClientWithVerifier(bool verifierReturns)
    {
        var verifier = Substitute.For<ITikTokHmacVerifier>();
        verifier.Verify(Arg.Any<string>(), Arg.Any<string>()).Returns(verifierReturns);

        var orderUseCase = Substitute.For<IProcessOrderWebhookUseCase>();
        orderUseCase
            .ExecuteAsync(Arg.Any<OrderWebhookContext>(), Arg.Any<CancellationToken>())
            .Returns(new ProcessOrderResult(ProcessOrderStatus.SkippedNoTenant));

        return _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["TikTokShop:ClientSecret"] = "test-secret"
                });
            });

            builder.ConfigureTestServices(services =>
            {
                // แทนที่ real verifier ด้วย mock
                var verifierDesc = services.SingleOrDefault(
                    d => d.ServiceType == typeof(ITikTokHmacVerifier));
                if (verifierDesc is not null) services.Remove(verifierDesc);
                services.AddScoped<ITikTokHmacVerifier>(_ => verifier);

                // แทนที่ real use case ด้วย mock
                var useCaseDesc = services.SingleOrDefault(
                    d => d.ServiceType == typeof(IProcessOrderWebhookUseCase));
                if (useCaseDesc is not null) services.Remove(useCaseDesc);
                services.AddScoped<IProcessOrderWebhookUseCase>(_ => orderUseCase);
            });
        }).CreateClient();
    }

    [Fact]
    public async Task ReceiveTikTokWebhook__WhenSignatureIsValid__ShouldReturn200()
    {
        // Arrange
        var client = CreateClientWithVerifier(verifierReturns: true);
        var payload = """{"client_key":"test-key","event":"ORDER_STATUS_CHANGE","create_time":1234567890,"content":"{}"}""";

        var request = new HttpRequestMessage(HttpMethod.Post, "/webhooks/tiktok-shop");
        request.Content = new StringContent(payload, Encoding.UTF8, "application/json");
        request.Headers.Add(TikTokShopOptions.SignatureHeaderName, "t=1234567890,s=fakesig");

        // Act
        var response = await client.SendAsync(request);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task ReceiveTikTokWebhook__WhenSignatureIsInvalid__ShouldReturn401()
    {
        // Arrange
        var client = CreateClientWithVerifier(verifierReturns: false);
        var payload = """{"client_key":"test-key","event":"ORDER_STATUS_CHANGE","create_time":1234567890,"content":"{}"}""";

        var request = new HttpRequestMessage(HttpMethod.Post, "/webhooks/tiktok-shop");
        request.Content = new StringContent(payload, Encoding.UTF8, "application/json");
        request.Headers.Add(TikTokShopOptions.SignatureHeaderName, "t=1234567890,s=wrong");

        // Act
        var response = await client.SendAsync(request);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ReceiveTikTokWebhook__WhenSignatureHeaderIsMissing__ShouldReturn401()
    {
        // Arrange
        var client = CreateClientWithVerifier(verifierReturns: false);
        var payload = """{"client_key":"test-key","event":"ORDER_STATUS_CHANGE","create_time":1234567890,"content":"{}"}""";

        var request = new HttpRequestMessage(HttpMethod.Post, "/webhooks/tiktok-shop");
        request.Content = new StringContent(payload, Encoding.UTF8, "application/json");
        // ไม่ใส่ signature header

        // Act
        var response = await client.SendAsync(request);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ReceiveTikTokWebhook__WhenPayloadIsInvalidJson__ShouldReturn422()
    {
        // Arrange
        var client = CreateClientWithVerifier(verifierReturns: true);

        var request = new HttpRequestMessage(HttpMethod.Post, "/webhooks/tiktok-shop");
        request.Content = new StringContent("not-json", Encoding.UTF8, "text/plain");
        request.Headers.Add(TikTokShopOptions.SignatureHeaderName, "t=1234567890,s=fakesig");

        // Act
        var response = await client.SendAsync(request);

        // Assert
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }
}
