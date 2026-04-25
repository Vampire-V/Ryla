using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Ryla.Core.Domain.Integrations;
using Ryla.Core.Ports.Outbound;
using Ryla.Core.UseCases;
using Xunit;

namespace Ryla.Core.Tests.UseCases;

public sealed class TestNotificationUseCaseTests
{
    private readonly IConnectionRepository _connections = Substitute.For<IConnectionRepository>();
    private readonly ILineNotifier _notifier = Substitute.For<ILineNotifier>();
    private readonly TestNotificationUseCase _sut;

    public TestNotificationUseCaseTests()
    {
        _sut = new TestNotificationUseCase(
            _connections,
            _notifier,
            NullLogger<TestNotificationUseCase>.Instance);
    }

    [Fact]
    public async Task ExecuteAsync__WhenLineCredentialsExist__ShouldSendPushAndReturnSuccess()
    {
        var tenantId = Guid.NewGuid();
        var creds = new LineCredentials("tok-abc", "Utest");

        _connections.GetLineCredentialsAsync(tenantId, Arg.Any<CancellationToken>()).Returns(creds);
        _notifier.PushAsync("tok-abc", "Utest", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new LineNotifyResult(true));

        var result = await _sut.ExecuteAsync(tenantId);

        Assert.True(result.Success);
        Assert.Null(result.ErrorMessage);
        await _notifier.Received(1).PushAsync("tok-abc", "Utest", Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync__WhenNoLineConnection__ShouldReturnFailureWithMessage()
    {
        _connections.GetLineCredentialsAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((LineCredentials?)null);

        var result = await _sut.ExecuteAsync(Guid.NewGuid());

        Assert.False(result.Success);
        Assert.NotNull(result.ErrorMessage);
        await _notifier.DidNotReceive().PushAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync__WhenLinePushFails__ShouldReturnFailureWithError()
    {
        var tenantId = Guid.NewGuid();
        _connections.GetLineCredentialsAsync(tenantId, Arg.Any<CancellationToken>())
            .Returns(new LineCredentials("tok", "U1"));
        _notifier.PushAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new LineNotifyResult(false, "http_401", "unauthorized"));

        var result = await _sut.ExecuteAsync(tenantId);

        Assert.False(result.Success);
        Assert.Contains("unauthorized", result.ErrorMessage);
    }
}
