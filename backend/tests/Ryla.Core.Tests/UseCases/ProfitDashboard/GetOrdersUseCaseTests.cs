using NSubstitute;
using Ryla.Core.Domain.Orders;
using Ryla.Core.Ports.Outbound;
using Ryla.Core.UseCases.ProfitDashboard;

namespace Ryla.Core.Tests.UseCases.ProfitDashboard;

public sealed class GetOrdersUseCaseTests
{
    private readonly IOrderRepository _orderRepo = Substitute.For<IOrderRepository>();
    private readonly IGetOrdersUseCase _sut;

    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly DateTimeOffset From = new(2025, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset To = new(2025, 1, 31, 23, 59, 59, TimeSpan.Zero);

    public GetOrdersUseCaseTests()
    {
        _sut = new GetOrdersUseCase(_orderRepo);
    }

    [Fact]
    public async Task ExecuteAsync__WhenCalled__ShouldDelegateToRepositoryWithSameParameters()
    {
        // Arrange
        var orders = new List<ShopeeOrder>
        {
            new(Guid.NewGuid(), TenantId, "SN-001", "COMPLETED", "synced",
                1000m, 50m, 20m, 10m, 300m, 250m,
                From, From.AddDays(1), From)
        };
        var expected = new OrdersPage(orders, TotalCount: 1);

        _orderRepo.GetOrdersAsync(TenantId, From, To, 1, 20, Arg.Any<CancellationToken>())
            .Returns(expected);

        // Act
        var result = await _sut.ExecuteAsync(TenantId, From, To, page: 1, pageSize: 20);

        // Assert
        Assert.Equal(expected, result);
        await _orderRepo.Received(1).GetOrdersAsync(TenantId, From, To, 1, 20, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync__WhenPageAndPageSizeProvided__ShouldPassThroughToRepository()
    {
        // Arrange
        var empty = new OrdersPage(Array.Empty<ShopeeOrder>(), TotalCount: 0);
        _orderRepo.GetOrdersAsync(TenantId, From, To, 5, 50, Arg.Any<CancellationToken>())
            .Returns(empty);

        // Act
        var result = await _sut.ExecuteAsync(TenantId, From, To, page: 5, pageSize: 50);

        // Assert
        await _orderRepo.Received(1).GetOrdersAsync(TenantId, From, To, 5, 50, Arg.Any<CancellationToken>());
        Assert.Equal(0, result.TotalCount);
    }
}
