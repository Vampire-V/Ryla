using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Ryla.Api.Extensions;
using Ryla.Infrastructure.Adapters.Database;

namespace Ryla.Api.Endpoints;

public static class HealthEndpoints
{
    private static readonly TimeSpan DbProbeTimeout = TimeSpan.FromSeconds(2);

    public static IEndpointRouteBuilder MapHealthEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/health", GetHealthAsync)
            .AllowAnonymous() // เปิด public เพื่อให้ load balancer / Azure probe ตรวจสอบได้
            .WithName("GetHealth")
            .WithSummary("Health check")
            .WithDescription("Returns service + database health. 503 if database unreachable.")
            .WithTags("Health")
            .Produces<HealthResponse>(StatusCodes.Status200OK)
            .Produces<HealthResponse>(StatusCodes.Status503ServiceUnavailable);

        return app;
    }

    /// <summary>Returns the current health status of the Ryla API + database.</summary>
    internal static async Task<IResult> GetHealthAsync(
        IDbConnectionFactory connectionFactory,
        ILogger<HealthResponse> logger,
        CancellationToken cancellationToken)
    {
        var dbStatus = await ProbeDbAsync(connectionFactory, logger, cancellationToken);
        var overall = dbStatus == "ok" ? "healthy" : "degraded";
        var response = new HealthResponse(overall, dbStatus, DateTimeOffset.UtcNow);

        var statusCode = dbStatus == "ok"
            ? StatusCodes.Status200OK
            : StatusCodes.Status503ServiceUnavailable;

        return Results.Json(response, RylaJsonContext.Default.HealthResponse, statusCode: statusCode);
    }

    private static async Task<string> ProbeDbAsync(
        IDbConnectionFactory factory,
        ILogger<HealthResponse> logger,
        CancellationToken cancellationToken)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(DbProbeTimeout);

        try
        {
            await using var connection = await factory.CreateAsync(cts.Token);
            await using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT 1";
            await cmd.ExecuteScalarAsync(cts.Token);
            return "ok";
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Health check: DB probe failed");
            return "unreachable";
        }
    }
}

/// <summary>API + database health status response.</summary>
/// <param name="Status">"healthy" if DB reachable, "degraded" otherwise.</param>
/// <param name="Db">"ok" or "unreachable".</param>
/// <param name="Timestamp">Current UTC server time.</param>
public record HealthResponse(string Status, string Db, DateTimeOffset Timestamp);
