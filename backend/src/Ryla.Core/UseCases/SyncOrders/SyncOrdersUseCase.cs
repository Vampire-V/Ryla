using Microsoft.Extensions.Logging;
using Ryla.Core.Domain.Orders;
using Ryla.Core.Ports.Outbound;
using Ryla.Core.UseCases.ShopeeOAuth;

namespace Ryla.Core.UseCases.SyncOrders;

internal sealed class SyncOrdersUseCase(
    IShopeeTokenPort tokenPort,
    IOrderRepository orderRepo,
    IShopeeOrderDetailPort detailPort,
    ISkuCostRepository skuCostRepo,
    IRefreshShopeeTokenUseCase refreshUseCase,
    ILogger<SyncOrdersUseCase> logger) : ISyncOrdersUseCase
{
    private static readonly TimeSpan RefreshThreshold = TimeSpan.FromMinutes(30);
    private const int BatchLimit = 100;

    public async Task<SyncOrdersResult> ExecuteAsync(CancellationToken ct = default)
    {
        var tenantIds = await tokenPort.GetTenantIdsWithTokenAsync(ct);
        var processed = 0;
        var errors = 0;
        var authErrors = 0;

        foreach (var tenantId in tenantIds)
        {
            var refreshResult = await refreshUseCase.ExecuteAsync(tenantId, ct);
            if (refreshResult == RefreshResult.AuthExpired)
            {
                logger.LogWarning("Shopee token expired for tenant={TenantId}", tenantId);
                await orderRepo.SetAuthErrorForTenantAsync(tenantId, ct);
                authErrors++;
                continue;
            }

            var token = await tokenPort.GetTokenAsync(tenantId, ct);
            if (token is null)
            {
                logger.LogWarning("No token found for tenant={TenantId} after refresh", tenantId);
                continue;
            }

            var pendingOrders = await orderRepo.GetPendingCompletedOrdersAsync(tenantId, BatchLimit, ct);
            foreach (var pending in pendingOrders)
            {
                try
                {
                    var (p, e) = await SyncOrderAsync(tenantId, token, pending, ct);
                    processed += p;
                    errors += e;
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Sync failed for tenant={TenantId} orderId={OrderId}", tenantId, pending.OrderId);
                    await orderRepo.SetSyncErrorAsync(pending.OrderId, ex.Message, ct);
                    errors++;
                }
            }
        }

        return new SyncOrdersResult(processed, errors, authErrors);
    }

    private async Task<(int Processed, int Errors)> SyncOrderAsync(
        Guid tenantId,
        ShopeeTokenSecrets token,
        PendingSyncOrder pending,
        CancellationToken ct)
    {
        var detail = await detailPort.GetOrderDetailAsync(token.ShopId, token.AccessToken, pending.OrderSn, ct);
        if (detail is null)
        {
            logger.LogWarning("Order detail not found: tenant={TenantId} orderSn={OrderSn}", tenantId, pending.OrderSn);
            await orderRepo.SetSyncErrorAsync(pending.OrderId, "Order detail not found", ct);
            return (0, 1);
        }

        var grossMargin = detail.EscrowTotalAmount
            - detail.EscrowShopeeCommission
            + detail.EscrowShipRebate
            - detail.EscrowVoucher
            - detail.EscrowPromotion;

        var itemSkus = detail.Items.Select(i => i.ItemSku).Distinct().ToList();
        var costMap = await skuCostRepo.GetCostMapAsync(tenantId, "shopee", itemSkus, ct);

        decimal? netMargin = null;
        if (costMap.Count > 0 && detail.Items.All(i => costMap.ContainsKey(i.ItemSku)))
        {
            var totalCogs = detail.Items.Sum(i => costMap[i.ItemSku] * i.Quantity);
            netMargin = grossMargin - totalCogs;
        }

        var items = detail.Items.Select(i => new OrderItem(
            Guid.NewGuid(), pending.OrderId, tenantId,
            i.ItemSku, i.ModelSku, i.ItemName, i.Quantity, i.ItemPrice)).ToList();

        await orderRepo.UpdateSyncedAsync(pending.OrderId, detail, grossMargin, netMargin, items, ct);

        logger.LogInformation(
            "Order synced: tenant={TenantId} orderSn={OrderSn} gross={Gross} net={Net}",
            tenantId, pending.OrderSn, grossMargin, netMargin);

        return (1, 0);
    }
}
