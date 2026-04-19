using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Ryla.Core.Domain.Integrations;
using Ryla.Core.Domain.Webhooks;
using Ryla.Core.Ports.Outbound;
using Ryla.Core.UseCases;

namespace Ryla.Core.Tests.UseCases;

public sealed class ProcessOrderWebhookUseCaseTests
{
    private readonly IConnectionRepository _connectionRepo = Substitute.For<IConnectionRepository>();
    private readonly ILineNotifier _lineNotifier = Substitute.For<ILineNotifier>();
    private readonly IProcessOrderWebhookUseCase _sut;

    public ProcessOrderWebhookUseCaseTests()
    {
        _sut = new ProcessOrderWebhookUseCase(
            _connectionRepo,
            _lineNotifier,
            NullLogger<ProcessOrderWebhookUseCase>.Instance);
    }

    [Fact]
    public async Task Execute__WhenTenantFoundAndLineConfigured__ShouldCallLineNotifierAndReturnSent()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var ctx = new OrderWebhookContext(Platform.TikTokShop, "shop-001", "ORD-123", "ORDER_STATUS_CHANGE");

        _connectionRepo.FindTenantIdByShopIdAsync(Platform.TikTokShop, "shop-001", Arg.Any<CancellationToken>())
            .Returns(tenantId);
        _connectionRepo.GetLineCredentialsAsync(tenantId, Arg.Any<CancellationToken>())
            .Returns(new LineCredentials("token-abc", "uid-xyz"));
        _lineNotifier.PushAsync("token-abc", "uid-xyz", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new LineNotifyResult(true));

        // Act
        var result = await _sut.ExecuteAsync(ctx);

        // Assert
        Assert.Equal(ProcessOrderStatus.Sent, result.Status);
        await _lineNotifier.Received(1).PushAsync(
            "token-abc", "uid-xyz", Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Execute__WhenTenantNotFound__ShouldReturnSkippedNoTenant()
    {
        // Arrange
        var ctx = new OrderWebhookContext(Platform.TikTokShop, "unknown-shop", "ORD-999", "ORDER_STATUS_CHANGE");
        _connectionRepo.FindTenantIdByShopIdAsync(Arg.Any<Platform>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((Guid?)null);

        // Act
        var result = await _sut.ExecuteAsync(ctx);

        // Assert
        Assert.Equal(ProcessOrderStatus.SkippedNoTenant, result.Status);
        await _lineNotifier.DidNotReceiveWithAnyArgs()
            .PushAsync(default!, default!, default!, default);
    }

    [Fact]
    public async Task Execute__WhenLineCredentialsMissing__ShouldReturnSkippedNoLineConfig()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var ctx = new OrderWebhookContext(Platform.Shopee, "shop-002", "ORD-456", "ORDER_STATUS_CHANGE");

        _connectionRepo.FindTenantIdByShopIdAsync(Platform.Shopee, "shop-002", Arg.Any<CancellationToken>())
            .Returns(tenantId);
        _connectionRepo.GetLineCredentialsAsync(tenantId, Arg.Any<CancellationToken>())
            .Returns((LineCredentials?)null);

        // Act
        var result = await _sut.ExecuteAsync(ctx);

        // Assert
        Assert.Equal(ProcessOrderStatus.SkippedNoLineConfig, result.Status);
        await _lineNotifier.DidNotReceiveWithAnyArgs()
            .PushAsync(default!, default!, default!, default);
    }

    [Fact]
    public async Task Execute__WhenLineNotifierReturnsSuccessFalse__ShouldReturnFailedLineDelivery()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var ctx = new OrderWebhookContext(Platform.TikTokShop, "shop-003", "ORD-789", "ORDER_STATUS_CHANGE");

        _connectionRepo.FindTenantIdByShopIdAsync(Platform.TikTokShop, "shop-003", Arg.Any<CancellationToken>())
            .Returns(tenantId);
        _connectionRepo.GetLineCredentialsAsync(tenantId, Arg.Any<CancellationToken>())
            .Returns(new LineCredentials("bad-token", "uid-xyz"));
        _lineNotifier.PushAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new LineNotifyResult(false, "http_401", "Invalid channel access token"));

        // Act
        var result = await _sut.ExecuteAsync(ctx);

        // Assert
        Assert.Equal(ProcessOrderStatus.FailedLineDelivery, result.Status);
        Assert.Equal("Invalid channel access token", result.Detail);
    }

    [Fact]
    public async Task Execute__WhenPlatformIsShopee__ShouldPassShopeeToRepository()
    {
        // Arrange
        var ctx = new OrderWebhookContext(Platform.Shopee, "shopee-shop-777", "ORD-S01", "ORDER_CONFIRMED");
        _connectionRepo.FindTenantIdByShopIdAsync(Arg.Any<Platform>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((Guid?)null);

        // Act
        await _sut.ExecuteAsync(ctx);

        // Assert
        await _connectionRepo.Received(1).FindTenantIdByShopIdAsync(
            Platform.Shopee, "shopee-shop-777", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Execute__WhenExecuted__MessageShouldContainPlatformAndOrderId()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var ctx = new OrderWebhookContext(Platform.TikTokShop, "shop-T1", "ORD-T999", "ORDER_STATUS_CHANGE");
        string? capturedMessage = null;

        _connectionRepo.FindTenantIdByShopIdAsync(Platform.TikTokShop, "shop-T1", Arg.Any<CancellationToken>())
            .Returns(tenantId);
        _connectionRepo.GetLineCredentialsAsync(tenantId, Arg.Any<CancellationToken>())
            .Returns(new LineCredentials("tok", "uid"));
        _lineNotifier.PushAsync(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Do<string>(m => capturedMessage = m), Arg.Any<CancellationToken>())
            .Returns(new LineNotifyResult(true));

        // Act
        await _sut.ExecuteAsync(ctx);

        // Assert
        Assert.NotNull(capturedMessage);
        Assert.Contains("TikTok Shop", capturedMessage);
        Assert.Contains("ORD-T999", capturedMessage);
    }
}
