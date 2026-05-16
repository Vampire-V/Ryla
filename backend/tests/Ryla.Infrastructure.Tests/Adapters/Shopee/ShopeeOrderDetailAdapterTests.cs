using Ryla.Core.Domain.Orders;
using Ryla.Infrastructure.Adapters.Shopee;

namespace Ryla.Infrastructure.Tests.Adapters.Shopee;

/// <summary>
/// Unit tests สำหรับ ShopeeOrderDetailAdapter.MapToDetail (internal static)
/// ทดสอบ escrow field mapping และ double → decimal conversion
/// </summary>
public sealed class ShopeeOrderDetailAdapterTests
{
    // ─── MapToDetail__ValidPayload ────────────────────────────────────────────

    [Fact]
    public void MapToDetail__ValidPayload__ShouldMapAllEscrowFields()
    {
        // Arrange
        var createTime = DateTimeOffset.UtcNow.AddDays(-1);
        var item = new ShopeeOrderApiItem(
            OrderSn: "TEST-ORDER-001",
            OrderStatus: "COMPLETED",
            CreateTime: createTime.ToUnixTimeSeconds(),
            EscrowAmountInfo: new ShopeeEscrowInfo(
                BuyerTotalAmount: 1500.75,
                AdjustedTotalEscrowAmount: 1400.00,
                EscrowShopeeCommission: 75.50,
                EscrowShippingRebate: 25.00,
                EscrowVoucherFromShopee: 10.00,
                EscrowVoucherFromSeller: 5.25,
                ShopeeDiscount: 8.00),
            ItemList: null);

        // Act
        var result = ShopeeOrderDetailAdapter.MapToDetail(item);

        // Assert — escrow fields
        Assert.Equal("TEST-ORDER-001", result.OrderSn);
        Assert.Equal("COMPLETED", result.Status);
        Assert.Equal(1500.75m, result.EscrowTotalAmount);
        Assert.Equal(75.50m, result.EscrowShopeeCommission);
        Assert.Equal(25.00m, result.EscrowShipRebate);
        // EscrowVoucher = EscrowVoucherFromShopee + EscrowVoucherFromSeller
        Assert.Equal(15.25m, result.EscrowVoucher);
        Assert.Equal(8.00m, result.EscrowPromotion);
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(createTime.ToUnixTimeSeconds()), result.OrderCreateTime);
    }

    [Fact]
    public void MapToDetail__ValidPayload__ShouldMapItems()
    {
        // Arrange
        var items = new List<ShopeeOrderApiItemDetail>
        {
            new(ItemSku: "SKU-001", ModelSku: "MODEL-A", ItemName: "สินค้า A", ModelQuantityPurchased: 2, ModelDiscountedPrice: 199.50),
            new(ItemSku: "SKU-002", ModelSku: null, ItemName: "สินค้า B", ModelQuantityPurchased: 1, ModelDiscountedPrice: 350.00),
            new(ItemSku: "SKU-003", ModelSku: "", ItemName: "สินค้า C", ModelQuantityPurchased: 3, ModelDiscountedPrice: 99.99),
        };
        var apiItem = MakeApiItem("ORDER-002", items);

        // Act
        var result = ShopeeOrderDetailAdapter.MapToDetail(apiItem);

        // Assert — items
        Assert.Equal(3, result.Items.Count);

        var itemA = result.Items[0];
        Assert.Equal("SKU-001", itemA.ItemSku);
        Assert.Equal("MODEL-A", itemA.ModelSku);
        Assert.Equal("สินค้า A", itemA.ItemName);
        Assert.Equal(2, itemA.Quantity);
        Assert.Equal(199.50m, itemA.ItemPrice);

        // ModelSku=null ควรเป็น null
        Assert.Null(result.Items[1].ModelSku);

        // ModelSku="" (empty string) ควร normalize เป็น null
        Assert.Null(result.Items[2].ModelSku);
    }

    [Fact]
    public void MapToDetail__VoucherCombination__ShouldSumBothVoucherSources()
    {
        // Arrange — shopee voucher 100, seller voucher 50 → total 150
        var apiItem = MakeApiItem("ORDER-VOUCHER",
            escrow: new ShopeeEscrowInfo(
                BuyerTotalAmount: 1000.0,
                AdjustedTotalEscrowAmount: 850.0,
                EscrowShopeeCommission: 0.0,
                EscrowShippingRebate: 0.0,
                EscrowVoucherFromShopee: 100.0,
                EscrowVoucherFromSeller: 50.0,
                ShopeeDiscount: 0.0));

        // Act
        var result = ShopeeOrderDetailAdapter.MapToDetail(apiItem);

        // Assert
        Assert.Equal(150m, result.EscrowVoucher);
    }

    // ─── MapToDetail__NullOptionalFields ─────────────────────────────────────

    [Fact]
    public void MapToDetail__NullEscrowAmountInfo__ShouldDefaultAllEscrowFieldsToZero()
    {
        // Arrange — EscrowAmountInfo = null (order ที่ยังไม่ได้ escrow)
        var apiItem = new ShopeeOrderApiItem(
            OrderSn: "NULL-ESCROW",
            OrderStatus: "READY_TO_SHIP",
            CreateTime: 1700000000L,
            EscrowAmountInfo: null,
            ItemList: null);

        // Act
        var result = ShopeeOrderDetailAdapter.MapToDetail(apiItem);

        // Assert — ทุก escrow field ควรเป็น 0, ไม่ throw
        Assert.Equal("NULL-ESCROW", result.OrderSn);
        Assert.Equal(0m, result.EscrowTotalAmount);
        Assert.Equal(0m, result.EscrowShopeeCommission);
        Assert.Equal(0m, result.EscrowShipRebate);
        Assert.Equal(0m, result.EscrowVoucher);
        Assert.Equal(0m, result.EscrowPromotion);
    }

    [Fact]
    public void MapToDetail__NullItemList__ShouldReturnEmptyItems()
    {
        // Arrange — ItemList = null
        var apiItem = MakeApiItem("NULL-ITEMS", items: null);

        // Act
        var result = ShopeeOrderDetailAdapter.MapToDetail(apiItem);

        // Assert — Items ไม่ null และเป็น empty list
        Assert.NotNull(result.Items);
        Assert.Empty(result.Items);
    }

    [Fact]
    public void MapToDetail__ZeroEscrowValues__ShouldMapAsZeroDecimal()
    {
        // Arrange — ทุก escrow field เป็น 0.0
        var apiItem = MakeApiItem("ZERO-ESCROW",
            escrow: new ShopeeEscrowInfo(0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0));

        // Act
        var result = ShopeeOrderDetailAdapter.MapToDetail(apiItem);

        // Assert
        Assert.Equal(0m, result.EscrowTotalAmount);
        Assert.Equal(0m, result.EscrowVoucher);
    }

    // ─── Helpers ──────────────────────────────────────────────────────────────

    private static ShopeeOrderApiItem MakeApiItem(
        string orderSn,
        IReadOnlyList<ShopeeOrderApiItemDetail>? items = null,
        ShopeeEscrowInfo? escrow = null) =>
        new(
            OrderSn: orderSn,
            OrderStatus: "COMPLETED",
            CreateTime: 1700000000L,
            EscrowAmountInfo: escrow ?? new ShopeeEscrowInfo(
                BuyerTotalAmount: 500.0,
                AdjustedTotalEscrowAmount: 450.0,
                EscrowShopeeCommission: 25.0,
                EscrowShippingRebate: 10.0,
                EscrowVoucherFromShopee: 5.0,
                EscrowVoucherFromSeller: 0.0,
                ShopeeDiscount: 0.0),
            ItemList: items);
}
