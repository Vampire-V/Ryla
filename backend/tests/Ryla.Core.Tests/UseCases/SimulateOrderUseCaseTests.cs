using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Ryla.Core.Domain.Integrations;
using Ryla.Core.Domain.Webhooks;
using Ryla.Core.Ports.Outbound;
using Ryla.Core.UseCases;
using Xunit;

namespace Ryla.Core.Tests.UseCases;

public sealed class SimulateOrderUseCaseTests
{
    private readonly IConnectionRepository _connections = Substitute.For<IConnectionRepository>();
    private readonly ILineNotifier _notifier = Substitute.For<ILineNotifier>();
    private readonly ISheetAppender _sheets = Substitute.For<ISheetAppender>();
    private readonly SimulateOrderUseCase _sut;

    public SimulateOrderUseCaseTests()
    {
        _sut = new SimulateOrderUseCase(
            _connections,
            _notifier,
            _sheets,
            NullLogger<SimulateOrderUseCase>.Instance);
    }

    [Fact]
    public async Task ExecuteAsync__WhenBothConfigured__ShouldPushLineAndAppendSheets()
    {
        var tenantId = Guid.NewGuid();
        var lineCreds = new LineCredentials("tok-abc", "Utest");
        var sheetsCreds = new GoogleSheetsCredentials("sheet-id", "Orders", "{}");

        _connections.GetLineCredentialsAsync(tenantId, Arg.Any<CancellationToken>()).Returns(lineCreds);
        _connections.GetGoogleSheetsCredentialsAsync(tenantId, Arg.Any<CancellationToken>()).Returns(sheetsCreds);
        _notifier.PushAsync("tok-abc", "Utest", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new LineNotifyResult(true));
        _sheets.AppendRowAsync(sheetsCreds, Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>())
            .Returns(new SheetAppendResult(true));

        var result = await _sut.ExecuteAsync(tenantId, Platform.TikTokShop);

        Assert.True(result.LineSuccess);
        Assert.True(result.SheetsSuccess);
        Assert.Null(result.LineError);
        Assert.Null(result.SheetsError);
        await _notifier.Received(1).PushAsync("tok-abc", "Utest", Arg.Any<string>(), Arg.Any<CancellationToken>());
        await _sheets.Received(1).AppendRowAsync(sheetsCreds, Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync__WhenNoLineConfig__ShouldReturnLineFailure()
    {
        var tenantId = Guid.NewGuid();

        _connections.GetLineCredentialsAsync(tenantId, Arg.Any<CancellationToken>()).Returns((LineCredentials?)null);
        _connections.GetGoogleSheetsCredentialsAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((GoogleSheetsCredentials?)null);

        var result = await _sut.ExecuteAsync(tenantId, Platform.TikTokShop);

        Assert.False(result.LineSuccess);
        Assert.False(result.SheetsSuccess);
        Assert.NotNull(result.LineError);
        await _notifier.DidNotReceive().PushAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync__WhenNoSheetsConfig__ShouldSucceedLineOnly()
    {
        var tenantId = Guid.NewGuid();
        var lineCreds = new LineCredentials("tok", "U1");

        _connections.GetLineCredentialsAsync(tenantId, Arg.Any<CancellationToken>()).Returns(lineCreds);
        _connections.GetGoogleSheetsCredentialsAsync(tenantId, Arg.Any<CancellationToken>())
            .Returns((GoogleSheetsCredentials?)null);
        _notifier.PushAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new LineNotifyResult(true));

        var result = await _sut.ExecuteAsync(tenantId, Platform.TikTokShop);

        Assert.True(result.LineSuccess);
        Assert.False(result.SheetsSuccess);
        await _sheets.DidNotReceive().AppendRowAsync(
            Arg.Any<GoogleSheetsCredentials>(), Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync__GeneratedOrderId__ShouldHaveSimPrefix()
    {
        var tenantId = Guid.NewGuid();
        _connections.GetLineCredentialsAsync(tenantId, Arg.Any<CancellationToken>())
            .Returns(new LineCredentials("tok", "U1"));
        _connections.GetGoogleSheetsCredentialsAsync(tenantId, Arg.Any<CancellationToken>())
            .Returns((GoogleSheetsCredentials?)null);
        _notifier.PushAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new LineNotifyResult(true));

        var result = await _sut.ExecuteAsync(tenantId, Platform.TikTokShop);

        Assert.StartsWith("SIM-", result.OrderId);
    }

    [Fact]
    public async Task ExecuteAsync__LineMessage__ShouldMatchRealOrderFormat()
    {
        var tenantId = Guid.NewGuid();
        _connections.GetLineCredentialsAsync(tenantId, Arg.Any<CancellationToken>())
            .Returns(new LineCredentials("tok", "U1"));
        _connections.GetGoogleSheetsCredentialsAsync(tenantId, Arg.Any<CancellationToken>())
            .Returns((GoogleSheetsCredentials?)null);

        string? capturedMessage = null;
        _notifier.PushAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Do<string>(m => capturedMessage = m), Arg.Any<CancellationToken>())
            .Returns(new LineNotifyResult(true));

        await _sut.ExecuteAsync(tenantId, Platform.TikTokShop);

        Assert.NotNull(capturedMessage);
        Assert.Contains("TikTok Shop", capturedMessage);
        Assert.Contains("SIM-", capturedMessage);
        Assert.Contains("ORDER_STATUS_CHANGE", capturedMessage);
    }
}
