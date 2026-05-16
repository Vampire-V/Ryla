using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Ryla.Core.Domain.Orders;
using Ryla.Core.Ports.Outbound;
using Ryla.Core.UseCases.ShopeeOAuth;

namespace Ryla.Core.Tests.UseCases.ShopeeOAuth;

public sealed class RefreshShopeeTokenUseCaseTests
{
    private readonly IShopeeTokenPort _tokenPort = Substitute.For<IShopeeTokenPort>();
    private readonly IShopeeOAuthPort _oauthPort = Substitute.For<IShopeeOAuthPort>();
    private readonly IRefreshShopeeTokenUseCase _sut;

    private static readonly Guid TenantId = Guid.NewGuid();
    private const long ShopId = 987654321L;

    public RefreshShopeeTokenUseCaseTests()
    {
        _sut = new RefreshShopeeTokenUseCase(
            _tokenPort,
            _oauthPort,
            NullLogger<RefreshShopeeTokenUseCase>.Instance);
    }

    [Fact]
    public async Task ExecuteAsync__WhenNoTokenExists__ShouldReturnNotRequired()
    {
        // Arrange
        _tokenPort.GetTokenAsync(TenantId, Arg.Any<CancellationToken>())
            .Returns((ShopeeTokenSecrets?)null);

        // Act
        var result = await _sut.ExecuteAsync(TenantId);

        // Assert
        Assert.Equal(RefreshResult.NotRequired, result);
        await _oauthPort.DidNotReceive().RefreshAccessTokenAsync(
            Arg.Any<long>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync__WhenTokenIsNotExpiringSoon__ShouldReturnNotRequired()
    {
        // Arrange — expires in 2 hours, well above 30-minute threshold
        var secrets = new ShopeeTokenSecrets(
            TenantId: TenantId,
            ShopId: ShopId,
            AccessToken: "valid-token",
            RefreshToken: "refresh-token",
            ExpiresAt: DateTimeOffset.UtcNow.AddHours(2));

        _tokenPort.GetTokenAsync(TenantId, Arg.Any<CancellationToken>())
            .Returns(secrets);

        // Act
        var result = await _sut.ExecuteAsync(TenantId);

        // Assert
        Assert.Equal(RefreshResult.NotRequired, result);
        await _oauthPort.DidNotReceive().RefreshAccessTokenAsync(
            Arg.Any<long>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync__WhenTokenExpiresWithin30Minutes__ShouldRefreshAndReturnRefreshed()
    {
        // Arrange — expires in 10 minutes, below 30-minute threshold
        var newExpiresAt = DateTimeOffset.UtcNow.AddHours(4);
        var secrets = new ShopeeTokenSecrets(
            TenantId: TenantId,
            ShopId: ShopId,
            AccessToken: "expiring-token",
            RefreshToken: "refresh-token",
            ExpiresAt: DateTimeOffset.UtcNow.AddMinutes(10));

        _tokenPort.GetTokenAsync(TenantId, Arg.Any<CancellationToken>())
            .Returns(secrets);

        _oauthPort.RefreshAccessTokenAsync(ShopId, "refresh-token", Arg.Any<CancellationToken>())
            .Returns(new ShopeeRefreshResponse("new-access-token", newExpiresAt));

        // Act
        var result = await _sut.ExecuteAsync(TenantId);

        // Assert
        Assert.Equal(RefreshResult.Refreshed, result);
        await _oauthPort.Received(1).RefreshAccessTokenAsync(ShopId, "refresh-token", Arg.Any<CancellationToken>());
        await _tokenPort.Received(1).UpdateAccessTokenAsync(TenantId, "new-access-token", newExpiresAt, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync__WhenRefreshTokenExpired__ShouldReturnAuthExpired()
    {
        // Arrange — token expiring soon, but refresh_token is expired on Shopee side
        var secrets = new ShopeeTokenSecrets(
            TenantId: TenantId,
            ShopId: ShopId,
            AccessToken: "expiring-token",
            RefreshToken: "expired-refresh-token",
            ExpiresAt: DateTimeOffset.UtcNow.AddMinutes(5));

        _tokenPort.GetTokenAsync(TenantId, Arg.Any<CancellationToken>())
            .Returns(secrets);

        _oauthPort.RefreshAccessTokenAsync(ShopId, "expired-refresh-token", Arg.Any<CancellationToken>())
            .ThrowsAsync(new ShopeeAuthExpiredException());

        // Act
        var result = await _sut.ExecuteAsync(TenantId);

        // Assert
        Assert.Equal(RefreshResult.AuthExpired, result);
        await _tokenPort.DidNotReceive().UpdateAccessTokenAsync(
            Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync__WhenUnexpectedExceptionThrown__ShouldPropagate()
    {
        // Arrange — token expiring soon, port throws unexpected error
        var secrets = new ShopeeTokenSecrets(
            TenantId: TenantId,
            ShopId: ShopId,
            AccessToken: "expiring-token",
            RefreshToken: "refresh-token",
            ExpiresAt: DateTimeOffset.UtcNow.AddMinutes(10));

        _tokenPort.GetTokenAsync(TenantId, Arg.Any<CancellationToken>())
            .Returns(secrets);

        _oauthPort.RefreshAccessTokenAsync(Arg.Any<long>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Network error"));

        // Act & Assert
        await Assert.ThrowsAsync<HttpRequestException>(() =>
            _sut.ExecuteAsync(TenantId));
    }
}
