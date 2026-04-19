using System.Net;
using System.Text;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Ryla.Core.Domain.Webhooks;
using Ryla.Core.Services;
using Ryla.Core.UseCases;
using Xunit;

namespace Ryla.Api.Tests.Endpoints;

public class ShopeeWebhookEndpointsTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public ShopeeWebhookEndpointsTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    private HttpClient CreateClientWithVerifier(bool verifierReturns)
    {
        var verifier = Substitute.For<IShopeeHmacVerifier>();
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
                    ["Shopee:PartnerKey"] = "test-partner-key",
                    ["Shopee:CallbackUrl"] = "http://localhost/webhooks/shopee",
                    ["Shopee:MaxAgeSeconds"] = "300"
                });
            });

            builder.ConfigureTestServices(services =>
            {
                var verifierDesc = services.SingleOrDefault(
                    d => d.ServiceType == typeof(IShopeeHmacVerifier));
                if (verifierDesc is not null) services.Remove(verifierDesc);
                services.AddScoped<IShopeeHmacVerifier>(_ => verifier);

                // แทนที่ real use case ด้วย mock
                var useCaseDesc = services.SingleOrDefault(
                    d => d.ServiceType == typeof(IProcessOrderWebhookUseCase));
                if (useCaseDesc is not null) services.Remove(useCaseDesc);
                services.AddScoped<IProcessOrderWebhookUseCase>(_ => orderUseCase);
            });
        }).CreateClient();
    }

    [Fact]
    public async Task ReceiveShopeeWebhook__WhenSignatureIsValid__ShouldReturn200()
    {
        var client = CreateClientWithVerifier(verifierReturns: true);
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var payload = $$$"""{"code":3,"shop_id":123456,"timestamp":{{{now}}},"data":{"ordersn":"TEST123","status":"READY_TO_SHIP","update_time":{{{now}}}}}""";

        var request = new HttpRequestMessage(HttpMethod.Post, "/webhooks/shopee?authorization=valid-sig");
        request.Content = new StringContent(payload, Encoding.UTF8, "application/json");

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task ReceiveShopeeWebhook__WhenSignatureIsInvalid__ShouldReturn401()
    {
        var client = CreateClientWithVerifier(verifierReturns: false);
        var payload = """{"code":3,"shop_id":123456,"timestamp":1000,"data":{}}""";

        var request = new HttpRequestMessage(HttpMethod.Post, "/webhooks/shopee?authorization=wrong-sig");
        request.Content = new StringContent(payload, Encoding.UTF8, "application/json");

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ReceiveShopeeWebhook__WhenAuthorizationParamIsMissing__ShouldReturn401()
    {
        var client = CreateClientWithVerifier(verifierReturns: false);
        var payload = """{"code":3,"shop_id":123456,"timestamp":1000,"data":{}}""";

        var request = new HttpRequestMessage(HttpMethod.Post, "/webhooks/shopee");
        request.Content = new StringContent(payload, Encoding.UTF8, "application/json");

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ReceiveShopeeWebhook__WhenPayloadIsInvalidJson__ShouldReturn422()
    {
        var client = CreateClientWithVerifier(verifierReturns: true);

        var request = new HttpRequestMessage(HttpMethod.Post, "/webhooks/shopee?authorization=valid-sig");
        request.Content = new StringContent("not-json", Encoding.UTF8, "text/plain");

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task ReceiveShopeeWebhook__WhenTimestampIsStale__ShouldReturn401()
    {
        var client = CreateClientWithVerifier(verifierReturns: true);
        var staleTimestamp = DateTimeOffset.UtcNow.AddMinutes(-10).ToUnixTimeSeconds();
        var payload = $$$"""{"code":3,"shop_id":123456,"timestamp":{{{staleTimestamp}}},"data":{"ordersn":"TEST123","status":"READY_TO_SHIP"}}""";

        var request = new HttpRequestMessage(HttpMethod.Post, "/webhooks/shopee?authorization=valid-sig");
        request.Content = new StringContent(payload, Encoding.UTF8, "application/json");

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ReceiveShopeeWebhook__WhenEventIsNotOrder__ShouldStillReturn200()
    {
        var client = CreateClientWithVerifier(verifierReturns: true);
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var payload = $$$"""{"code":0,"shop_id":123456,"timestamp":{{{now}}},"data":{"shop_id":123456}}""";

        var request = new HttpRequestMessage(HttpMethod.Post, "/webhooks/shopee?authorization=valid-sig");
        request.Content = new StringContent(payload, Encoding.UTF8, "application/json");

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task ReceiveShopeeWebhook__WhenBodyIsEmpty__ShouldReturn401()
    {
        var client = CreateClientWithVerifier(verifierReturns: false);

        var request = new HttpRequestMessage(HttpMethod.Post, "/webhooks/shopee?authorization=valid-sig");
        request.Content = new StringContent("", Encoding.UTF8, "application/json");

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ReceiveShopeeWebhook__WhenTimestampIsFuture__ShouldReturn401()
    {
        var client = CreateClientWithVerifier(verifierReturns: true);
        var futureTimestamp = DateTimeOffset.UtcNow.AddMinutes(10).ToUnixTimeSeconds();
        var payload = $$$"""{"code":3,"shop_id":123456,"timestamp":{{{futureTimestamp}}},"data":{"ordersn":"TEST123","status":"READY_TO_SHIP"}}""";

        var request = new HttpRequestMessage(HttpMethod.Post, "/webhooks/shopee?authorization=valid-sig");
        request.Content = new StringContent(payload, Encoding.UTF8, "application/json");

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ReceiveShopeeWebhook__WhenPayloadIsJsonNull__ShouldReturn422()
    {
        var client = CreateClientWithVerifier(verifierReturns: true);

        var request = new HttpRequestMessage(HttpMethod.Post, "/webhooks/shopee?authorization=valid-sig");
        request.Content = new StringContent("null", Encoding.UTF8, "application/json");

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }
}
