using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Ryla.Api.Endpoints;

namespace Ryla.Api.Tests.Endpoints;

public class HealthEndpointsTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public HealthEndpointsTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
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
