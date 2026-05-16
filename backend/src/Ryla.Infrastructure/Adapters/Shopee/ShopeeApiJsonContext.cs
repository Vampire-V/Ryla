using System.Text.Json.Serialization;

namespace Ryla.Infrastructure.Adapters.Shopee;

// ─── OAuth response types ──────────────────────────────────────────────────

internal sealed record ShopeeTokenExchangeResponse(
    [property: JsonPropertyName("access_token")] string AccessToken,
    [property: JsonPropertyName("refresh_token")] string RefreshToken,
    [property: JsonPropertyName("expire_in")] int ExpireIn,
    [property: JsonPropertyName("shop_id")] long ShopId,
    [property: JsonPropertyName("error")] string? Error,
    [property: JsonPropertyName("message")] string? Message);

internal sealed record ShopeeRefreshTokenApiResponse(
    [property: JsonPropertyName("access_token")] string AccessToken,
    [property: JsonPropertyName("expire_in")] int ExpireIn,
    [property: JsonPropertyName("error")] string? Error,
    [property: JsonPropertyName("message")] string? Message);

// ─── Order detail response types ──────────────────────────────────────────

internal sealed record ShopeeOrderDetailApiResponse(
    [property: JsonPropertyName("error")] string? Error,
    [property: JsonPropertyName("message")] string? Message,
    [property: JsonPropertyName("response")] ShopeeOrderDetailResponseBody? Response);

internal sealed record ShopeeOrderDetailResponseBody(
    [property: JsonPropertyName("order_list")] IReadOnlyList<ShopeeOrderApiItem>? OrderList);

internal sealed record ShopeeOrderApiItem(
    [property: JsonPropertyName("order_sn")] string OrderSn,
    [property: JsonPropertyName("order_status")] string OrderStatus,
    [property: JsonPropertyName("create_time")] long CreateTime,
    [property: JsonPropertyName("escrow_amount_info")] ShopeeEscrowInfo? EscrowAmountInfo,
    [property: JsonPropertyName("item_list")] IReadOnlyList<ShopeeOrderApiItemDetail>? ItemList);

internal sealed record ShopeeEscrowInfo(
    [property: JsonPropertyName("buyer_total_amount")] double BuyerTotalAmount,
    [property: JsonPropertyName("adjusted_total_escrow_amount")] double AdjustedTotalEscrowAmount,
    [property: JsonPropertyName("escrow_shopee_commission")] double EscrowShopeeCommission,
    [property: JsonPropertyName("escrow_shipping_rebate")] double EscrowShippingRebate,
    [property: JsonPropertyName("escrow_voucher_from_shopee")] double EscrowVoucherFromShopee,
    [property: JsonPropertyName("escrow_voucher_from_seller")] double EscrowVoucherFromSeller,
    [property: JsonPropertyName("shopee_discount")] double ShopeeDiscount);

internal sealed record ShopeeOrderApiItemDetail(
    [property: JsonPropertyName("item_sku")] string ItemSku,
    [property: JsonPropertyName("model_sku")] string? ModelSku,
    [property: JsonPropertyName("item_name")] string ItemName,
    [property: JsonPropertyName("model_quantity_purchased")] int ModelQuantityPurchased,
    [property: JsonPropertyName("model_discounted_price")] double ModelDiscountedPrice);

// ─── JSON Source Generator (AOT-safe) ─────────────────────────────────────

[JsonSerializable(typeof(ShopeeTokenExchangeRequest))]
[JsonSerializable(typeof(ShopeeTokenExchangeResponse))]
[JsonSerializable(typeof(ShopeeRefreshTokenRequest))]
[JsonSerializable(typeof(ShopeeRefreshTokenApiResponse))]
[JsonSerializable(typeof(ShopeeOrderDetailApiResponse))]
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower)]
internal sealed partial class ShopeeApiJsonContext : JsonSerializerContext;

// ─── Request types ─────────────────────────────────────────────────────────

internal sealed record ShopeeTokenExchangeRequest(
    [property: JsonPropertyName("code")] string Code,
    [property: JsonPropertyName("shop_id")] long ShopId,
    [property: JsonPropertyName("partner_id")] long PartnerId);

internal sealed record ShopeeRefreshTokenRequest(
    [property: JsonPropertyName("refresh_token")] string RefreshToken,
    [property: JsonPropertyName("shop_id")] long ShopId,
    [property: JsonPropertyName("partner_id")] long PartnerId);
