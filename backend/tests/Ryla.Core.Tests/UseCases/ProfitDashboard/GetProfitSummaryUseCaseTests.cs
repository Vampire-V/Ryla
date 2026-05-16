using NSubstitute;
using Ryla.Core.Domain.Orders;
using Ryla.Core.Ports.Outbound;
using Ryla.Core.UseCases.ProfitDashboard;

namespace Ryla.Core.Tests.UseCases.ProfitDashboard;

public sealed class GetProfitSummaryUseCaseTests
{
    private readonly IOrderRepository _orderRepo = Substitute.For<IOrderRepository>();
    private readonly IGetProfitSummaryUseCase _sut;

    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly DateTimeOffset From = new(2025, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset To = new(2025, 1, 31, 23, 59, 59, TimeSpan.Zero);

    public GetProfitSummaryUseCaseTests()
    {
        _sut = new GetProfitSummaryUseCase(_orderRepo);
    }

    [Fact]
    public async Task ExecuteAsync__WhenOrdersExist__ShouldReturnSummaryFromRepository()
    {
        // Arrange
        var expected = new ProfitSummary(
            TotalRevenue: 10_000m,
            TotalGrossMargin: 3_000m,
            TotalNetMargin: 2_500m,
            GrossMarginPercent: 30m,
            NetMarginPercent: 25m,
            CompletedOrderCount: 42,
            PendingSettlementCount: 3);

        _orderRepo.GetSummaryAsync(TenantId, From, To, Arg.Any<CancellationToken>())
            .Returns(expected);

        // Act
        var result = await _sut.ExecuteAsync(TenantId, From, To);

        // Assert
        Assert.Equal(expected, result);
        await _orderRepo.Received(1).GetSummaryAsync(TenantId, From, To, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync__WhenTenantHasNoOrders__ShouldReturnEmptySummary()
    {
        // Arrange
        var empty = ProfitSummary.Empty();
        _orderRepo.GetSummaryAsync(TenantId, From, To, Arg.Any<CancellationToken>())
            .Returns(empty);

        // Act
        var result = await _sut.ExecuteAsync(TenantId, From, To);

        // Assert
        Assert.Equal(0m, result.TotalRevenue);
        Assert.Equal(0, result.CompletedOrderCount);
        Assert.Null(result.GrossMarginPercent);
        Assert.Null(result.NetMarginPercent);
    }
}
