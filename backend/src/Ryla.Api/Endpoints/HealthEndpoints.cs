using Microsoft.AspNetCore.Http.HttpResults;

namespace Ryla.Api.Endpoints;

public static class HealthEndpoints
{
    public static IEndpointRouteBuilder MapHealthEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/health", GetHealth)
            .AllowAnonymous() // เปิด public เพื่อให้ load balancer / Azure probe ตรวจสอบได้
            .WithName("GetHealth")
            .WithSummary("Health check")
            .WithDescription("Returns service health status and server timestamp. Used by Azure Functions health probes and load balancers.")
            .WithTags("Health")
            .Produces<HealthResponse>(StatusCodes.Status200OK);
        // หมายเหตุ: ไม่ใช้ .WithOpenApi() — มี [RequiresDynamicCode] ซึ่ง AOT-incompatible
        // AddOpenApi() + MapOpenApi() ใน Program.cs collect metadata จาก WithSummary/WithTags/Produces อัตโนมัติ

        return app;
    }

    /// <summary>Returns the current health status of the Ryla API.</summary>
    /// <returns>Health status with UTC timestamp.</returns>
    internal static Ok<HealthResponse> GetHealth() =>
        TypedResults.Ok(new HealthResponse("healthy", DateTimeOffset.UtcNow));
}

/// <summary>API health status response.</summary>
/// <param name="Status">Always "healthy" when the service is operational.</param>
/// <param name="Timestamp">Current UTC server time.</param>
public record HealthResponse(string Status, DateTimeOffset Timestamp);
