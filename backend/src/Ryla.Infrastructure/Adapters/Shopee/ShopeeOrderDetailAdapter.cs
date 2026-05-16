using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Ryla.Core.Configuration;
using Ryla.Core.Domain.Orders;
using Ryla.Core.Ports.Outbound;

namespace Ryla.Infrastructure.Adapters.Shopee;

/// <summary>
/// Shopee get_order_detail API adapter
/// ดึง escrow financial data สำหรับ COMPLETED orders
/// </summary>
internal sealed class ShopeeOrderDetailAdapter(
    HttpClient httpClient,
    IOptions<ShopeeOptions> options,
    ILogger<ShopeeOrderDetailAdapter> logger) : IShopeeOrderDetailPort
{
    private const string OrderDetailPath = "/api/v2/order/get_order_detail";

    [ExcludeFromCodeCoverage] // HTTP + Shopee API — covered by E2E tests
    public async Task<ShopeeOrderDetail?> GetOrderDetailAsync(
        long shopId, string accessToken, string orderSn,
        CancellationToken ct = default)
    {
        var opts = options.Value;
        var timestamp = ShopeeApiSignature.UnixTimestamp();
        var sign = ShopeeApiSignature.ComputeShopSign(
            opts.PartnerId, OrderDetailPath, timestamp, accessToken, shopId, opts.PartnerKey);

        // response_optional_fields=escrow_amount ต้องระบุเพื่อให้ Shopee return escrow_amount_info
        var url = $"{opts.ApiBaseUrl}{OrderDetailPath}" +
                  $"?partner_id={opts.PartnerId}" +
                  $"&timestamp={timestamp}" +
                  $"&sign={sign}" +
                  $"&access_token={accessToken}" +
                  $"&shop_id={shopId}" +
                  $"&order_sn_list={orderSn}" +
                  $"&response_optional_fields=escrow_amount";

        using var response = await httpClient.GetAsync(url, ct);

        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning(
                "Shopee get_order_detail HTTP {StatusCode} for orderSn={OrderSn}",
                (int)response.StatusCode, orderSn);
            return null;
        }

        var responseJson = await response.Content.ReadAsStringAsync(ct);
        var result = JsonSerializer.Deserialize(
            responseJson, ShopeeApiJsonContext.Default.ShopeeOrderDetailApiResponse);

        if (result is null || !string.IsNullOrEmpty(result.Error))
        {
            logger.LogWarning(
                "Shopee get_order_detail error={Error} msg={Message} for orderSn={OrderSn}",
                result?.Error, result?.Message, orderSn);
            return null;
        }

        var order = result.Response?.OrderList?.FirstOrDefault(o => o.OrderSn == orderSn);
        if (order is null)
        {
            logger.LogWarning("Order not found in Shopee response: orderSn={OrderSn}", orderSn);
            return null;
        }

        return MapToDetail(order);
    }

    internal static ShopeeOrderDetail MapToDetail(ShopeeOrderApiItem order)
    {
        var escrow = order.EscrowAmountInfo;
        return new ShopeeOrderDetail(
            OrderSn: order.OrderSn,
            Status: order.OrderStatus,
            // buyer_total_amount = gross revenue ก่อนหัก fees
            EscrowTotalAmount: (decimal)(escrow?.BuyerTotalAmount ?? 0),
            EscrowShopeeCommission: (decimal)(escrow?.EscrowShopeeCommission ?? 0),
            EscrowShipRebate: (decimal)(escrow?.EscrowShippingRebate ?? 0),
            EscrowVoucher: (decimal)((escrow?.EscrowVoucherFromShopee ?? 0) + (escrow?.EscrowVoucherFromSeller ?? 0)),
            EscrowPromotion: (decimal)(escrow?.ShopeeDiscount ?? 0),
            OrderCreateTime: DateTimeOffset.FromUnixTimeSeconds(order.CreateTime),
            Items: (order.ItemList ?? []).Select(i => new ShopeeOrderItemDetail(
                ItemSku: i.ItemSku,
                ModelSku: string.IsNullOrEmpty(i.ModelSku) ? null : i.ModelSku,
                ItemName: i.ItemName,
                Quantity: i.ModelQuantityPurchased,
                ItemPrice: (decimal)i.ModelDiscountedPrice)).ToList());
    }
}
