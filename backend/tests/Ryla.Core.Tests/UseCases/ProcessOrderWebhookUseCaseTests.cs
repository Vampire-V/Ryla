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
    private readonly ISheetAppender _sheetAppender = Substitute.For<ISheetAppender>();
    private readonly IProcessOrderWebhookUseCase _sut;

    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly GoogleSheetsCredentials SheetsCredentials =
        new("spreadsheet-id", "Sheet1", """{"client_email":"svc@test.iam","private_key":"KEY"}""");

    public ProcessOrderWebhookUseCaseTests()
    {
        _sut = new ProcessOrderWebhookUseCase(
            _connectionRepo,
            _lineNotifier,
            _sheetAppender,
            NullLogger<ProcessOrderWebhookUseCase>.Instance);
    }

    // ─── Existing tests (updated for new ctor/types) ──────────────────────────

    [Fact]
    public async Task Execute__WhenTenantFoundAndLineConfigured__ShouldCallLineNotifierAndReturnSent()
    {
        // Arrange
        var ctx = new OrderWebhookContext(Platform.TikTokShop, "shop-001", "ORD-123", "ORDER_STATUS_CHANGE");

        _connectionRepo.FindTenantIdByShopIdAsync(Platform.TikTokShop, "shop-001", Arg.Any<CancellationToken>())
            .Returns(TenantId);
        _connectionRepo.GetLineCredentialsAsync(TenantId, Arg.Any<CancellationToken>())
            .Returns(new LineCredentials("token-abc", "uid-xyz"));
        _connectionRepo.GetGoogleSheetsCredentialsAsync(TenantId, Arg.Any<CancellationToken>())
            .Returns((GoogleSheetsCredentials?)null);
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
        await _sheetAppender.DidNotReceiveWithAnyArgs()
            .AppendRowAsync(default!, default!, default);
    }

    [Fact]
    public async Task Execute__WhenLineCredentialsMissing__ShouldReturnSkippedNoLineConfig()
    {
        // Arrange
        var ctx = new OrderWebhookContext(Platform.Shopee, "shop-002", "ORD-456", "ORDER_STATUS_CHANGE");

        _connectionRepo.FindTenantIdByShopIdAsync(Platform.Shopee, "shop-002", Arg.Any<CancellationToken>())
            .Returns(TenantId);
        _connectionRepo.GetLineCredentialsAsync(TenantId, Arg.Any<CancellationToken>())
            .Returns((LineCredentials?)null);
        _connectionRepo.GetGoogleSheetsCredentialsAsync(TenantId, Arg.Any<CancellationToken>())
            .Returns((GoogleSheetsCredentials?)null);

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
        var ctx = new OrderWebhookContext(Platform.TikTokShop, "shop-003", "ORD-789", "ORDER_STATUS_CHANGE");

        _connectionRepo.FindTenantIdByShopIdAsync(Platform.TikTokShop, "shop-003", Arg.Any<CancellationToken>())
            .Returns(TenantId);
        _connectionRepo.GetLineCredentialsAsync(TenantId, Arg.Any<CancellationToken>())
            .Returns(new LineCredentials("bad-token", "uid-xyz"));
        _connectionRepo.GetGoogleSheetsCredentialsAsync(TenantId, Arg.Any<CancellationToken>())
            .Returns((GoogleSheetsCredentials?)null);
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
        var ctx = new OrderWebhookContext(Platform.TikTokShop, "shop-T1", "ORD-T999", "ORDER_STATUS_CHANGE");
        string? capturedMessage = null;

        _connectionRepo.FindTenantIdByShopIdAsync(Platform.TikTokShop, "shop-T1", Arg.Any<CancellationToken>())
            .Returns(TenantId);
        _connectionRepo.GetLineCredentialsAsync(TenantId, Arg.Any<CancellationToken>())
            .Returns(new LineCredentials("tok", "uid"));
        _connectionRepo.GetGoogleSheetsCredentialsAsync(TenantId, Arg.Any<CancellationToken>())
            .Returns((GoogleSheetsCredentials?)null);
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

    // ─── New Sheets tests ─────────────────────────────────────────────────────

    [Fact]
    public async Task Execute__WhenSheetsCredsAbsent__ShouldStillSendLineAndSucceed()
    {
        // Arrange
        var ctx = new OrderWebhookContext(Platform.TikTokShop, "shop-A", "ORD-A1", "ORDER_STATUS_CHANGE");

        _connectionRepo.FindTenantIdByShopIdAsync(Platform.TikTokShop, "shop-A", Arg.Any<CancellationToken>())
            .Returns(TenantId);
        _connectionRepo.GetLineCredentialsAsync(TenantId, Arg.Any<CancellationToken>())
            .Returns(new LineCredentials("tok", "uid"));
        _connectionRepo.GetGoogleSheetsCredentialsAsync(TenantId, Arg.Any<CancellationToken>())
            .Returns((GoogleSheetsCredentials?)null);
        _lineNotifier.PushAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new LineNotifyResult(true));

        // Act
        var result = await _sut.ExecuteAsync(ctx);

        // Assert: LINE sent, Sheets skipped, overall Sent
        Assert.Equal(ProcessOrderStatus.Sent, result.Status);
        Assert.Equal(LineDeliveryStatus.Sent, result.LineStatus);
        Assert.Equal(SheetsDeliveryStatus.SkippedNoConfig, result.SheetsStatus);
        await _lineNotifier.Received(1).PushAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        await _sheetAppender.DidNotReceiveWithAnyArgs().AppendRowAsync(default!, default!, default);
    }

    [Fact]
    public async Task Execute__WhenSheetsAppendFails__ShouldStillReportLineSentAndLogWarning()
    {
        // Arrange
        var ctx = new OrderWebhookContext(Platform.Shopee, "shop-B", "ORD-B2", "ORDER_STATUS_CHANGE");

        _connectionRepo.FindTenantIdByShopIdAsync(Platform.Shopee, "shop-B", Arg.Any<CancellationToken>())
            .Returns(TenantId);
        _connectionRepo.GetLineCredentialsAsync(TenantId, Arg.Any<CancellationToken>())
            .Returns(new LineCredentials("tok", "uid"));
        _connectionRepo.GetGoogleSheetsCredentialsAsync(TenantId, Arg.Any<CancellationToken>())
            .Returns(SheetsCredentials);
        _lineNotifier.PushAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new LineNotifyResult(true));
        _sheetAppender.AppendRowAsync(SheetsCredentials, Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>())
            .Returns(new SheetAppendResult(false, "upstream_error", "Google API error"));

        // Act
        var result = await _sut.ExecuteAsync(ctx);

        // Assert: LINE sent, Sheets failed — overall still Sent (fail-soft)
        Assert.Equal(ProcessOrderStatus.Sent, result.Status);
        Assert.Equal(LineDeliveryStatus.Sent, result.LineStatus);
        Assert.Equal(SheetsDeliveryStatus.Failed, result.SheetsStatus);
    }

    [Fact]
    public async Task Execute__WhenBothChannelsSucceed__ShouldReturnBothSent()
    {
        // Arrange
        var ctx = new OrderWebhookContext(Platform.TikTokShop, "shop-C", "ORD-C3", "ORDER_CONFIRMED");

        _connectionRepo.FindTenantIdByShopIdAsync(Platform.TikTokShop, "shop-C", Arg.Any<CancellationToken>())
            .Returns(TenantId);
        _connectionRepo.GetLineCredentialsAsync(TenantId, Arg.Any<CancellationToken>())
            .Returns(new LineCredentials("tok", "uid"));
        _connectionRepo.GetGoogleSheetsCredentialsAsync(TenantId, Arg.Any<CancellationToken>())
            .Returns(SheetsCredentials);
        _lineNotifier.PushAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new LineNotifyResult(true));
        _sheetAppender.AppendRowAsync(SheetsCredentials, Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>())
            .Returns(new SheetAppendResult(true));

        // Act
        var result = await _sut.ExecuteAsync(ctx);

        // Assert
        Assert.Equal(ProcessOrderStatus.Sent, result.Status);
        Assert.Equal(LineDeliveryStatus.Sent, result.LineStatus);
        Assert.Equal(SheetsDeliveryStatus.Sent, result.SheetsStatus);
    }

    [Fact]
    public async Task Execute__WhenSheetsSucceeds__ShouldPassCorrectRowValues()
    {
        // Arrange
        var ctx = new OrderWebhookContext(Platform.Shopee, "shop-D", "ORD-D4", "ORDER_CONFIRMED");
        IReadOnlyList<string>? capturedRow = null;

        _connectionRepo.FindTenantIdByShopIdAsync(Platform.Shopee, "shop-D", Arg.Any<CancellationToken>())
            .Returns(TenantId);
        _connectionRepo.GetLineCredentialsAsync(TenantId, Arg.Any<CancellationToken>())
            .Returns(new LineCredentials("tok", "uid"));
        _connectionRepo.GetGoogleSheetsCredentialsAsync(TenantId, Arg.Any<CancellationToken>())
            .Returns(SheetsCredentials);
        _lineNotifier.PushAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new LineNotifyResult(true));
        _sheetAppender
            .AppendRowAsync(SheetsCredentials, Arg.Do<IReadOnlyList<string>>(r => capturedRow = r), Arg.Any<CancellationToken>())
            .Returns(new SheetAppendResult(true));

        // Act
        await _sut.ExecuteAsync(ctx);

        // Assert: row = [utc_iso, platform, orderId, eventType]
        Assert.NotNull(capturedRow);
        Assert.Equal(4, capturedRow.Count);
        Assert.Equal("Shopee", capturedRow[1]);
        Assert.Equal("ORD-D4", capturedRow[2]);
        Assert.Equal("ORDER_CONFIRMED", capturedRow[3]);
    }

    [Fact]
    public async Task Execute__WhenLineFailsButSheetsSucceeds__ShouldReturnSheetsOnly()
    {
        // Arrange
        var ctx = new OrderWebhookContext(Platform.TikTokShop, "shop-E", "ORD-E5", "ORDER_STATUS_CHANGE");

        _connectionRepo.FindTenantIdByShopIdAsync(Platform.TikTokShop, "shop-E", Arg.Any<CancellationToken>())
            .Returns(TenantId);
        _connectionRepo.GetLineCredentialsAsync(TenantId, Arg.Any<CancellationToken>())
            .Returns(new LineCredentials("bad-tok", "uid"));
        _connectionRepo.GetGoogleSheetsCredentialsAsync(TenantId, Arg.Any<CancellationToken>())
            .Returns(SheetsCredentials);
        _lineNotifier.PushAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new LineNotifyResult(false, "http_401", "bad token"));
        _sheetAppender.AppendRowAsync(SheetsCredentials, Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>())
            .Returns(new SheetAppendResult(true));

        // Act
        var result = await _sut.ExecuteAsync(ctx);

        // Assert: Sheets sent → overall Sent; LINE failed
        Assert.Equal(ProcessOrderStatus.Sent, result.Status);
        Assert.Equal(LineDeliveryStatus.Failed, result.LineStatus);
        Assert.Equal(SheetsDeliveryStatus.Sent, result.SheetsStatus);
    }
}
