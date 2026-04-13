using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Ryla.Api.Endpoints;
using Ryla.Infrastructure.Adapters.Database;

namespace Ryla.Api.Tests.Endpoints;

public class HealthEndpointsTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public HealthEndpointsTests(WebApplicationFactory<Program> factory)
    {
        // Health endpoint ไม่ใช้ DB เลย — แทน IDbConnectionFactory ด้วย stub
        // เพื่อให้ startup ผ่านโดยไม่ต้องมี connection string จริง
        _client = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                var descriptor = services.SingleOrDefault(
                    d => d.ServiceType == typeof(IDbConnectionFactory));
                if (descriptor is not null)
                    services.Remove(descriptor);
                services.AddSingleton(Substitute.For<IDbConnectionFactory>());
            });
        }).CreateClient();
    }

    [Fact]
    public async Task GetHealth__WhenCalled__ShouldReturnOkWithHealthyStatus()
    {
        var response = await _client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetHealth__WhenCalled__ShouldReturnHealthyStatusInBody()
    {
        var response = await _client.GetFromJsonAsync<HealthResponse>("/health");

        Assert.NotNull(response);
        Assert.Equal("healthy", response.Status);
    }

    [Fact]
    public async Task GetHealth__WhenCalled__ShouldReturnRecentTimestamp()
    {
        var before = DateTimeOffset.UtcNow;

        var response = await _client.GetFromJsonAsync<HealthResponse>("/health");

        Assert.NotNull(response);
        Assert.True(response.Timestamp >= before);
        Assert.True(response.Timestamp <= DateTimeOffset.UtcNow);
    }
}
