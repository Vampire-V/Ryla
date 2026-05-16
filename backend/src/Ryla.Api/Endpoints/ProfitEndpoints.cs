using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Ryla.Api.Extensions;
using Ryla.Core.Domain.Orders;
using Ryla.Core.Ports.Outbound;
using Ryla.Core.UseCases.ProfitDashboard;
using Ryla.Core.UseCases.SkuCosts;

namespace Ryla.Api.Endpoints;

// ─── Request/Response records ─────────────────────────────────────────────────

public sealed record UpsertSkuCostRequest(
    [property: JsonPropertyName("itemSku")] string ItemSku,
    [property: JsonPropertyName("itemName")] string? ItemName,
    [property: JsonPropertyName("cogs")] decimal Cogs);

public sealed record OrdersResponse(
    [property: JsonPropertyName("orders")] ShopeeOrder[] Orders,
    [property: JsonPropertyName("totalCount")] int TotalCount,
    [property: JsonPropertyName("page")] int Page,
    [property: JsonPropertyName("pageSize")] int PageSize);

public sealed record SkuCostsResponse(
    [property: JsonPropertyName("items")] SkuCost[] Items);

// ─── Endpoints ────────────────────────────────────────────────────────────────

public static class ProfitEndpoints
{
    private const string Platform = "shopee";

    public static IEndpointRouteBuilder MapProfitEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/profit").WithTags("Profit Dashboard");

        group.MapGet("/summary", GetSummaryAsync)
            .WithName("GetProfitSummary")
            .WithSummary("KPI cards — revenue, gross/net margin สำหรับ COMPLETED orders")
            .Produces<ProfitSummary>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest);

        group.MapGet("/orders", GetOrdersAsync)
            .WithName("GetOrders")
            .WithSummary("Paginated orders list (all statuses)")
            .Produces<OrdersResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest);

        group.MapGet("/sku-costs", GetSkuCostsAsync)
            .WithName("GetSkuCosts")
            .WithSummary("ดึง COGS ทั้งหมดของ tenant")
            .Produces<SkuCostsResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest);

        group.MapPost("/sku-costs", UpsertSkuCostAsync)
            .WithName("UpsertSkuCost")
            .WithSummary("เพิ่ม/อัปเดต COGS ต่อ item_sku")
            .Produces<SkuCost>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status422UnprocessableEntity);

        group.MapGet("/sync-status", GetSyncStatusAsync)
            .WithName("GetSyncStatus")
            .WithSummary("Sync progress สำหรับ onboarding wizard (polling endpoint)")
            .Produces<SyncProgress>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest);

        return app;
    }

    internal static async Task<IResult> GetSummaryAsync(
        HttpContext ctx,
        IGetProfitSummaryUseCase useCase,
        string? range = "30d",
        string? from = null,
        string? to = null,
        CancellationToken ct = default)
    {
        if (!TryGetTenantId(ctx, out var tenantId))
            return Results.BadRequest(new { error = "X-Tenant-Id header missing or invalid" });

        var (dateFrom, dateTo) = ParseDateRange(range, from, to);
        var summary = await useCase.ExecuteAsync(tenantId, dateFrom, dateTo, ct);
        return Results.Json(summary, RylaJsonContext.Default.ProfitSummary);
    }

    internal static async Task<IResult> GetOrdersAsync(
        HttpContext ctx,
        IGetOrdersUseCase useCase,
        string? range = "30d",
        string? from = null,
        string? to = null,
        int page = 1,
        int limit = 50,
        CancellationToken ct = default)
    {
        if (!TryGetTenantId(ctx, out var tenantId))
            return Results.BadRequest(new { error = "X-Tenant-Id header missing or invalid" });

        limit = Math.Clamp(limit, 1, 100);
        page = Math.Max(1, page);
        var (dateFrom, dateTo) = ParseDateRange(range, from, to);

        var result = await useCase.ExecuteAsync(tenantId, dateFrom, dateTo, page, limit, ct);
        var response = new OrdersResponse(
            result.Orders.ToArray(), result.TotalCount, page, limit);
        return Results.Json(response, RylaJsonContext.Default.OrdersResponse);
    }

    internal static async Task<IResult> GetSkuCostsAsync(
        HttpContext ctx,
        IGetSkuCostsUseCase useCase,
        CancellationToken ct = default)
    {
        if (!TryGetTenantId(ctx, out var tenantId))
            return Results.BadRequest(new { error = "X-Tenant-Id header missing or invalid" });

        var costs = await useCase.ExecuteAsync(tenantId, Platform, ct);
        return Results.Json(new SkuCostsResponse(costs.ToArray()), RylaJsonContext.Default.SkuCostsResponse);
    }

    internal static async Task<IResult> UpsertSkuCostAsync(
        HttpContext ctx,
        UpsertSkuCostRequest request,
        IUpsertSkuCostUseCase useCase,
        CancellationToken ct = default)
    {
        if (!TryGetTenantId(ctx, out var tenantId))
            return Results.BadRequest(new { error = "X-Tenant-Id header missing or invalid" });

        if (string.IsNullOrWhiteSpace(request.ItemSku))
            return Results.UnprocessableEntity(new { error = "itemSku is required" });

        if (request.Cogs <= 0)
            return Results.UnprocessableEntity(new { error = "cogs must be greater than 0" });

        var result = await useCase.ExecuteAsync(
            tenantId, Platform, request.ItemSku, request.ItemName, request.Cogs, ct);
        return Results.Json(result, RylaJsonContext.Default.SkuCost);
    }

    internal static async Task<IResult> GetSyncStatusAsync(
        HttpContext ctx,
        IGetSyncStatusUseCase useCase,
        CancellationToken ct = default)
    {
        if (!TryGetTenantId(ctx, out var tenantId))
            return Results.BadRequest(new { error = "X-Tenant-Id header missing or invalid" });

        var progress = await useCase.ExecuteAsync(tenantId, ct);
        return Results.Json(progress, RylaJsonContext.Default.SyncProgress);
    }

    // ─── Helpers ──────────────────────────────────────────────────────────────

    private static bool TryGetTenantId(HttpContext ctx, out Guid tenantId)
    {
        var headerValue = ctx.Request.Headers["X-Tenant-Id"].FirstOrDefault();
        return Guid.TryParse(headerValue, out tenantId);
    }

    private static (DateTimeOffset From, DateTimeOffset To) ParseDateRange(
        string? range, string? from, string? to)
    {
        if (!string.IsNullOrEmpty(from) && !string.IsNullOrEmpty(to)
            && DateTimeOffset.TryParse(from, out var fromDate)
            && DateTimeOffset.TryParse(to, out var toDate))
        {
            return (fromDate.ToUniversalTime(), toDate.ToUniversalTime());
        }

        var days = range switch { "7d" => 7, "90d" => 90, _ => 30 };
        var now = DateTimeOffset.UtcNow;
        return (now.AddDays(-days), now);
    }
}
