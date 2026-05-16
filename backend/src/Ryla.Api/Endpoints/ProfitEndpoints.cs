using System.Security.Claims;
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
        // RequireAuthorization() บังคับ JWT บนทุก endpoint ในกลุ่มนี้
        var group = app.MapGroup("/api/profit")
            .WithTags("Profit Dashboard")
            .RequireAuthorization();

        group.MapGet("/summary", GetSummaryAsync)
            .WithName("GetProfitSummary")
            .WithSummary("KPI cards — revenue, gross/net margin สำหรับ COMPLETED orders")
            .Produces<ProfitSummary>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden);

        group.MapGet("/orders", GetOrdersAsync)
            .WithName("GetOrders")
            .WithSummary("Paginated orders list (all statuses)")
            .Produces<OrdersResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden);

        group.MapGet("/sku-costs", GetSkuCostsAsync)
            .WithName("GetSkuCosts")
            .WithSummary("ดึง COGS ทั้งหมดของ tenant")
            .Produces<SkuCostsResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden);

        group.MapPost("/sku-costs", UpsertSkuCostAsync)
            .WithName("UpsertSkuCost")
            .WithSummary("เพิ่ม/อัปเดต COGS ต่อ item_sku")
            .Produces<SkuCost>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status422UnprocessableEntity);

        group.MapGet("/sync-status", GetSyncStatusAsync)
            .WithName("GetSyncStatus")
            .WithSummary("Sync progress สำหรับ onboarding wizard (polling endpoint)")
            .Produces<SyncProgress>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden);

        return app;
    }

    internal static async Task<IResult> GetSummaryAsync(
        HttpContext ctx,
        IGetProfitSummaryUseCase useCase,
        ITenantResolver tenantResolver,
        string? range = "30d",
        string? from = null,
        string? to = null,
        CancellationToken ct = default)
    {
        var tenantResult = await ResolveTenantAsync(ctx, tenantResolver, ct);
        if (!tenantResult.IsSuccess)
            return tenantResult.Error!;

        var (dateFrom, dateTo) = ParseDateRange(range, from, to);
        var summary = await useCase.ExecuteAsync(tenantResult.TenantId, dateFrom, dateTo, ct);
        return Results.Json(summary, RylaJsonContext.Default.ProfitSummary);
    }

    internal static async Task<IResult> GetOrdersAsync(
        HttpContext ctx,
        IGetOrdersUseCase useCase,
        ITenantResolver tenantResolver,
        string? range = "30d",
        string? from = null,
        string? to = null,
        int page = 1,
        int limit = 50,
        CancellationToken ct = default)
    {
        var tenantResult = await ResolveTenantAsync(ctx, tenantResolver, ct);
        if (!tenantResult.IsSuccess)
            return tenantResult.Error!;

        limit = Math.Clamp(limit, 1, 100);
        page = Math.Max(1, page);
        var (dateFrom, dateTo) = ParseDateRange(range, from, to);

        var result = await useCase.ExecuteAsync(tenantResult.TenantId, dateFrom, dateTo, page, limit, ct);
        var response = new OrdersResponse(
            result.Orders.ToArray(), result.TotalCount, page, limit);
        return Results.Json(response, RylaJsonContext.Default.OrdersResponse);
    }

    internal static async Task<IResult> GetSkuCostsAsync(
        HttpContext ctx,
        IGetSkuCostsUseCase useCase,
        ITenantResolver tenantResolver,
        CancellationToken ct = default)
    {
        var tenantResult = await ResolveTenantAsync(ctx, tenantResolver, ct);
        if (!tenantResult.IsSuccess)
            return tenantResult.Error!;

        var costs = await useCase.ExecuteAsync(tenantResult.TenantId, Platform, ct);
        return Results.Json(new SkuCostsResponse(costs.ToArray()), RylaJsonContext.Default.SkuCostsResponse);
    }

    internal static async Task<IResult> UpsertSkuCostAsync(
        HttpContext ctx,
        UpsertSkuCostRequest request,
        IUpsertSkuCostUseCase useCase,
        ITenantResolver tenantResolver,
        CancellationToken ct = default)
    {
        var tenantResult = await ResolveTenantAsync(ctx, tenantResolver, ct);
        if (!tenantResult.IsSuccess)
            return tenantResult.Error!;

        if (string.IsNullOrWhiteSpace(request.ItemSku))
            return Results.UnprocessableEntity();

        if (request.Cogs <= 0)
            return Results.UnprocessableEntity();

        var result = await useCase.ExecuteAsync(
            tenantResult.TenantId, Platform, request.ItemSku, request.ItemName, request.Cogs, ct);
        return Results.Json(result, RylaJsonContext.Default.SkuCost);
    }

    internal static async Task<IResult> GetSyncStatusAsync(
        HttpContext ctx,
        IGetSyncStatusUseCase useCase,
        ITenantResolver tenantResolver,
        CancellationToken ct = default)
    {
        var tenantResult = await ResolveTenantAsync(ctx, tenantResolver, ct);
        if (!tenantResult.IsSuccess)
            return tenantResult.Error!;

        var progress = await useCase.ExecuteAsync(tenantResult.TenantId, ct);
        return Results.Json(progress, RylaJsonContext.Default.SyncProgress);
    }

    // ─── Helpers ──────────────────────────────────────────────────────────────

    private sealed record TenantResolutionResult(bool IsSuccess, Guid TenantId, IResult? Error);

    /// <summary>
    /// Resolve tenant_id จาก JWT sub claim — cache ไว้ใน HttpContext.Items
    /// ไม่อ่านจาก X-Tenant-Id header อีกต่อไป (แก้ IDOR vulnerability)
    /// </summary>
    private static async ValueTask<TenantResolutionResult> ResolveTenantAsync(
        HttpContext ctx,
        ITenantResolver tenantResolver,
        CancellationToken ct)
    {
        // Cache per-request เพื่อไม่ query DB ซ้ำถ้าเรียกหลายครั้งในหนึ่ง request
        if (ctx.Items.TryGetValue("TenantId", out var cached) && cached is Guid cachedId)
            return new TenantResolutionResult(true, cachedId, null);

        // ดึง user_id จาก JWT sub claim (MapInboundClaims = false → claim type = "sub")
        var sub = ctx.User.FindFirstValue("sub");
        if (!Guid.TryParse(sub, out var userId))
            return new TenantResolutionResult(false, Guid.Empty, Results.Forbid());

        var result = await tenantResolver.ResolveAsync(userId, ct);
        if (!result.IsSuccess)
            return new TenantResolutionResult(false, Guid.Empty, Results.Forbid());

        ctx.Items["TenantId"] = result.Value;
        return new TenantResolutionResult(true, (Guid)result.Value!, null);
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
