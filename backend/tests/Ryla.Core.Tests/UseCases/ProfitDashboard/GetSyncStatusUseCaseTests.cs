using NSubstitute;
using Ryla.Core.Ports.Outbound;
using Ryla.Core.UseCases.ProfitDashboard;

namespace Ryla.Core.Tests.UseCases.ProfitDashboard;

public sealed class GetSyncStatusUseCaseTests
{
    private readonly IOrderRepository _orderRepo = Substitute.For<IOrderRepository>();
    private readonly IGetSyncStatusUseCase _sut;

    private static readonly Guid TenantId = Guid.NewGuid();

    public GetSyncStatusUseCaseTests()
    {
        _sut = new GetSyncStatusUseCase(_orderRepo);
    }

    [Fact]
    public async Task ExecuteAsync__WhenCalled__ShouldReturnProgressFromRepository()
    {
        // Arrange
        var expected = new SyncProgress(Synced: 80, Pending: 20, Total: 100);
        _orderRepo.GetSyncProgressAsync(TenantId, Arg.Any<CancellationToken>())
            .Returns(expected);

        // Act
        var result = await _sut.ExecuteAsync(TenantId);

        // Assert
        Assert.Equal(expected, result);
        await _orderRepo.Received(1).GetSyncProgressAsync(TenantId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync__WhenNoOrdersSynced__ShouldReturnZeroProgress()
    {
        // Arrange
        var expected = new SyncProgress(Synced: 0, Pending: 0, Total: 0);
        _orderRepo.GetSyncProgressAsync(TenantId, Arg.Any<CancellationToken>())
            .Returns(expected);

        // Act
        var result = await _sut.ExecuteAsync(TenantId);

        // Assert
        Assert.Equal(0, result.Synced);
        Assert.Equal(0, result.Total);
    }
}
