using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Ryla.Core.Ports.Outbound;
using Ryla.Core.UseCases.StoreOrderFromWebhook;

namespace Ryla.Core.Tests.UseCases;

public sealed class StoreOrderFromWebhookUseCaseTests
{
    private readonly IOrderRepository _orderRepo = Substitute.For<IOrderRepository>();
    private readonly IStoreOrderFromWebhookUseCase _sut;

    private static readonly Guid TenantId = Guid.NewGuid();

    public StoreOrderFromWebhookUseCaseTests()
    {
        _sut = new StoreOrderFromWebhookUseCase(_orderRepo);
    }

    [Fact]
    public async Task ExecuteAsync__WhenCalled__ShouldUpsertOrderWithCorrectPlatform()
    {
        // Act
        await _sut.ExecuteAsync(TenantId, "SN-001", "READY_TO_SHIP");

        // Assert
        await _orderRepo.Received(1).UpsertFromWebhookAsync(
            TenantId,
            "shopee",
            "SN-001",
            "READY_TO_SHIP",
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync__WhenRepoThrows__ShouldPropagateException()
    {
        // Arrange
        _orderRepo.UpsertFromWebhookAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("DB error"));

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _sut.ExecuteAsync(TenantId, "SN-001", "READY_TO_SHIP"));
    }
}
