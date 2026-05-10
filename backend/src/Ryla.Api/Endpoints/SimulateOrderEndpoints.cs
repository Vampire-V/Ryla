using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Ryla.Core.Domain.Webhooks;
using Ryla.Core.UseCases;

namespace Ryla.Api.Endpoints;

public record SimulateOrderRequest(string TenantId, string Platform = "tiktok_shop");
public record SimulateOrderResponse(bool LineSuccess, bool SheetsSuccess, string OrderId, string? Error);

public static class SimulateOrderEndpoints
{
    public static IEndpointRouteBuilder MapSimulateOrderEndpoints(
        this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/simulate-order", async (
            SimulateOrderRequest request,
            ISimulateOrderUseCase useCase,
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
                return Results.BadRequest(new SimulateOrderResponse(false, false, string.Empty, "Invalid tenantId format"));

            var platform = request.Platform.ToLowerInvariant() switch
            {
                "shopee" => Platform.Shopee,
                _ => Platform.TikTokShop
            };

            var result = await useCase.ExecuteAsync(tenantId, platform, ct);

            return Results.Ok(new SimulateOrderResponse(
                result.LineSuccess,
                result.SheetsSuccess,
                result.OrderId,
                result.LineSuccess ? null : result.LineError));
        })
            .WithName("PostSimulateOrder")
            .WithSummary("Simulate a new order event through the full pipeline (LINE + Sheets)")
            .WithTags("Simulator")
            .Produces<SimulateOrderResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces<SimulateOrderResponse>(StatusCodes.Status400BadRequest);

        return app;
    }
}
