using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Ryla.Core.Domain.Orders;
using Ryla.Core.Ports.Outbound;
using Ryla.Core.UseCases.ShopeeOAuth;
using Ryla.Core.UseCases.SyncOrders;

namespace Ryla.Core.Tests.UseCases;

public sealed class SyncOrdersUseCaseTests
{
    private readonly IShopeeTokenPort _tokenPort = Substitute.For<IShopeeTokenPort>();
    private readonly IOrderRepository _orderRepo = Substitute.For<IOrderRepository>();
    private readonly IShopeeOrderDetailPort _detailPort = Substitute.For<IShopeeOrderDetailPort>();
    private readonly ISkuCostRepository _skuCostRepo = Substitute.For<ISkuCostRepository>();
    private readonly IRefreshShopeeTokenUseCase _refreshUseCase = Substitute.For<IRefreshShopeeTokenUseCase>();
    private readonly ISyncOrdersUseCase _sut;

    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly ShopeeTokenSecrets ValidToken = new(
        TenantId, 12345L, "access-token", "refresh-token",
        DateTimeOffset.UtcNow.AddHours(3));

    public SyncOrdersUseCaseTests()
    {
        _sut = new SyncOrdersUseCase(
            _tokenPort,
            _orderRepo,
            _detailPort,
            _skuCostRepo,
            _refreshUseCase,
            NullLogger<SyncOrdersUseCase>.Instance);
    }

    [Fact]
    public async Task ExecuteAsync__WhenOrderIsCompleted__ShouldCallGetOrderDetailAndUpdateSynced()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        _tokenPort.GetTenantIdsWithTokenAsync(Arg.Any<CancellationToken>())
            .Returns([TenantId]);
        _refreshUseCase.ExecuteAsync(TenantId, Arg.Any<CancellationToken>())
            .Returns(RefreshResult.NotRequired);
        _tokenPort.GetTokenAsync(TenantId, Arg.Any<CancellationToken>())
            .Returns(ValidToken);
        _orderRepo.GetPendingCompletedOrdersAsync(TenantId, Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns([new PendingSyncOrder(orderId, "SN-001")]);

        var detail = new ShopeeOrderDetail(
            "SN-001", "COMPLETED",
            EscrowTotalAmount: 1000m,
            EscrowShopeeCommission: 50m,
            EscrowShipRebate: 20m,
            EscrowVoucher: 30m,
            EscrowPromotion: 10m,
            DateTimeOffset.UtcNow,
            Items: [new ShopeeOrderItemDetail("SKU-A", null, "Product A", 2, 450m)]);
        _detailPort.GetOrderDetailAsync(12345L, "access-token", "SN-001", Arg.Any<CancellationToken>())
            .Returns(detail);
        _skuCostRepo.GetCostMapAsync(TenantId, "shopee", Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<string, decimal> { ["SKU-A"] = 200m });

        // Act
        var result = await _sut.ExecuteAsync();

        // Assert: processed = 1, no errors
        Assert.Equal(1, result.Processed);
        Assert.Equal(0, result.Errors);
        Assert.Equal(0, result.AuthErrors);

        // gross_margin = 1000 - 50 + 20 - 30 - 10 = 930
        // net_margin = 930 - (200 × 2) = 530
        await _orderRepo.Received(1).UpdateSyncedAsync(
            orderId,
            detail,
            930m,
            530m,
            Arg.Any<IReadOnlyList<OrderItem>>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync__WhenNoCogs__ShouldSetNetMarginNull()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        _tokenPort.GetTenantIdsWithTokenAsync(Arg.Any<CancellationToken>())
            .Returns([TenantId]);
        _refreshUseCase.ExecuteAsync(TenantId, Arg.Any<CancellationToken>())
            .Returns(RefreshResult.NotRequired);
        _tokenPort.GetTokenAsync(TenantId, Arg.Any<CancellationToken>())
            .Returns(ValidToken);
        _orderRepo.GetPendingCompletedOrdersAsync(TenantId, Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns([new PendingSyncOrder(orderId, "SN-002")]);

        var detail = new ShopeeOrderDetail(
            "SN-002", "COMPLETED",
            500m, 25m, 10m, 0m, 0m,
            DateTimeOffset.UtcNow,
            [new ShopeeOrderItemDetail("SKU-B", null, "Product B", 1, 500m)]);
        _detailPort.GetOrderDetailAsync(Arg.Any<long>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(detail);
        _skuCostRepo.GetCostMapAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<string, decimal>()); // ไม่มี COGS

        // Act
        await _sut.ExecuteAsync();

        // Assert: net_margin = null (ไม่ใช่ 0)
        await _orderRepo.Received(1).UpdateSyncedAsync(
            orderId,
            detail,
            485m, // 500 - 25 + 10
            null, // ไม่มี COGS
            Arg.Any<IReadOnlyList<OrderItem>>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync__WhenAuthExpired__ShouldSetAuthErrorAndCountIt()
    {
        // Arrange
        _tokenPort.GetTenantIdsWithTokenAsync(Arg.Any<CancellationToken>())
            .Returns([TenantId]);
        _refreshUseCase.ExecuteAsync(TenantId, Arg.Any<CancellationToken>())
            .Returns(RefreshResult.AuthExpired);

        // Act
        var result = await _sut.ExecuteAsync();

        // Assert
        Assert.Equal(0, result.Processed);
        Assert.Equal(1, result.AuthErrors);
        await _orderRepo.Received(1).SetAuthErrorForTenantAsync(TenantId, Arg.Any<CancellationToken>());
        await _detailPort.DidNotReceive().GetOrderDetailAsync(Arg.Any<long>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync__WhenGetOrderDetailFails__ShouldSetSyncErrorAndContinue()
    {
        // Arrange
        var orderId1 = Guid.NewGuid();
        var orderId2 = Guid.NewGuid();
        _tokenPort.GetTenantIdsWithTokenAsync(Arg.Any<CancellationToken>())
            .Returns([TenantId]);
        _refreshUseCase.ExecuteAsync(TenantId, Arg.Any<CancellationToken>())
            .Returns(RefreshResult.NotRequired);
        _tokenPort.GetTokenAsync(TenantId, Arg.Any<CancellationToken>())
            .Returns(ValidToken);
        _orderRepo.GetPendingCompletedOrdersAsync(TenantId, Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns([new PendingSyncOrder(orderId1, "SN-FAIL"), new PendingSyncOrder(orderId2, "SN-OK")]);

        _detailPort.GetOrderDetailAsync(12345L, "access-token", "SN-FAIL", Arg.Any<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("timeout"));

        var okDetail = new ShopeeOrderDetail("SN-OK", "COMPLETED", 200m, 10m, 5m, 0m, 0m, DateTimeOffset.UtcNow,
            []);
        _detailPort.GetOrderDetailAsync(12345L, "access-token", "SN-OK", Arg.Any<CancellationToken>())
            .Returns(okDetail);
        _skuCostRepo.GetCostMapAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<string, decimal>());

        // Act
        var result = await _sut.ExecuteAsync();

        // Assert: 1 processed, 1 error — did not stop processing
        Assert.Equal(1, result.Processed);
        Assert.Equal(1, result.Errors);
        await _orderRepo.Received(1).SetSyncErrorAsync(orderId1, Arg.Any<string>(), Arg.Any<CancellationToken>());
    }
}
