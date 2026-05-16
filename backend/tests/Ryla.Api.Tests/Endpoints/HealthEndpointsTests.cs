using System.Collections.Generic;
using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using NSubstitute;
using Ryla.Api.Endpoints;
using Ryla.Infrastructure.Adapters.Database;

namespace Ryla.Api.Tests.Endpoints;

public class HealthEndpointsTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public HealthEndpointsTests(WebApplicationFactory<Program> factory) => _factory = factory;

    private HttpClient CreateClientWith(IDbConnectionFactory dbFactory) =>
        _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Supabase:JwtSecret"] = "ryla-test-jwt-secret-32-chars-min!",
                    ["ConnectionStrings:Supabase"] = "Host=localhost;Port=54322;Database=postgres;Username=postgres;Password=postgres"
                });
            });

            builder.ConfigureTestServices(services =>
            {
                // แทน IDbConnectionFactory ให้ควบคุม DB-probe behavior ใน test
                var descriptor = services.SingleOrDefault(
                    d => d.ServiceType == typeof(IDbConnectionFactory));
                if (descriptor is not null)
                    services.Remove(descriptor);
                services.AddSingleton(dbFactory);

                // ปิด DbStartupProbe ใน test (IHostedService startup ไม่ต้อง run)
                var probeDescriptors = services
                    .Where(d => d.ImplementationType == typeof(DbStartupProbe))
                    .ToList();
                foreach (var d in probeDescriptors)
                    services.Remove(d);
            });
        }).CreateClient();

    private static IDbConnectionFactory FactoryThatThrows()
    {
        var factory = Substitute.For<IDbConnectionFactory>();
        factory
            .CreateAsync(Arg.Any<CancellationToken>())
            .Returns<ValueTask<NpgsqlConnection>>(_ =>
                throw new NpgsqlException("connection refused"));
        return factory;
    }

    [Fact]
    public async Task GetHealth__WhenDbUnreachable__ShouldReturn503Degraded()
    {
        var client = CreateClientWith(FactoryThatThrows());

        var response = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<HealthResponse>();
        Assert.NotNull(body);
        Assert.Equal("degraded", body.Status);
        Assert.Equal("unreachable", body.Db);
    }

    [Fact]
    public async Task GetHealth__WhenCalled__ShouldReturnRecentTimestamp()
    {
        var before = DateTimeOffset.UtcNow.AddSeconds(-1);
        var client = CreateClientWith(FactoryThatThrows());

        var response = await client.GetAsync("/health");
        var body = await response.Content.ReadFromJsonAsync<HealthResponse>();

        Assert.NotNull(body);
        Assert.True(body.Timestamp >= before);
        Assert.True(body.Timestamp <= DateTimeOffset.UtcNow.AddSeconds(1));
    }
}
