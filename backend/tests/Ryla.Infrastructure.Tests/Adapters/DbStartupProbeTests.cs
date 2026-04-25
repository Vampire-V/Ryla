using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using NSubstitute;
using Ryla.Infrastructure.Adapters.Database;
using Ryla.Infrastructure.Tests.Shared;

namespace Ryla.Infrastructure.Tests.Adapters;

[Collection(nameof(PostgresCollection))]
public sealed class DbStartupProbeTests
{
    private readonly PostgresFixture _fixture;

    public DbStartupProbeTests(PostgresFixture fixture) => _fixture = fixture;

    private static IConfiguration EmptyConfig() =>
        new ConfigurationBuilder().Build();

    [Fact]
    public async Task StartAsync__WhenDbReachable__ShouldCompleteWithoutThrowing()
    {
        var factory = new NpgsqlConnectionFactory(_fixture.ConnectionString);
        var probe = new DbStartupProbe(
            factory,
            NullLogger<DbStartupProbe>.Instance,
            EmptyConfig());

        await probe.StartAsync(CancellationToken.None);
    }

    [Fact]
    public async Task StartAsync__WhenDbUnreachable__ShouldThrowInvalidOperationException()
    {
        var factory = Substitute.For<IDbConnectionFactory>();
        factory
            .CreateAsync(Arg.Any<CancellationToken>())
            .Returns<ValueTask<NpgsqlConnection>>(_ =>
                throw new NpgsqlException("connection refused"));

        var probe = new DbStartupProbe(
            factory,
            NullLogger<DbStartupProbe>.Instance,
            EmptyConfig());

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => probe.StartAsync(CancellationToken.None));

        Assert.Contains("Database unreachable", ex.Message);
        Assert.IsType<NpgsqlException>(ex.InnerException);
    }

    [Fact]
    public async Task StartAsync__WhenTimeoutExceeded__ShouldThrowInvalidOperationException()
    {
        var factory = Substitute.For<IDbConnectionFactory>();
        factory
            .CreateAsync(Arg.Any<CancellationToken>())
            .Returns<ValueTask<NpgsqlConnection>>(async call =>
            {
                // Simulate slow DB — honor the CancellationToken so probe timeout wins
                var ct = call.Arg<CancellationToken>();
                await Task.Delay(TimeSpan.FromSeconds(10), ct);
                throw new InvalidOperationException("should have been cancelled");
            });

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["STARTUP_DB_PROBE_TIMEOUT_SECONDS"] = "1"
            })
            .Build();

        var probe = new DbStartupProbe(factory, NullLogger<DbStartupProbe>.Instance, config);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => probe.StartAsync(CancellationToken.None));
    }
}
