using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Ryla.Core.UseCases;

namespace Ryla.Api.Endpoints;

public record TestNotificationRequest(string TenantId);
public record TestNotificationResponse(bool Success, string Message);

public static class TestNotificationEndpoints
{
    public static IEndpointRouteBuilder MapTestNotificationEndpoints(
        this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/test-notification", async (
            TestNotificationRequest request,
            ITestNotificationUseCase useCase,
            HttpContext ctx,
            IConfiguration config,
            CancellationToken ct) =>
        {
            var expectedSecret = config["InternalApiSecret"];
            if (string.IsNullOrEmpty(expectedSecret)
                || !ctx.Request.Headers.TryGetValue("X-Internal-Secret", out var secret)
                || secret != expectedSecret)
            {
                return Results.Unauthorized();
            }

            if (!Guid.TryParse(request.TenantId, out var tenantId))
                return Results.BadRequest("Invalid tenantId format");

            var result = await useCase.ExecuteAsync(tenantId, ct);

            return Results.Ok(new TestNotificationResponse(
                result.Success,
                result.Success
                    ? "ส่งสำเร็จ! เช็ค LINE ของคุณ"
                    : result.ErrorMessage ?? "เกิดข้อผิดพลาด"));
        });

        return app;
    }
}
