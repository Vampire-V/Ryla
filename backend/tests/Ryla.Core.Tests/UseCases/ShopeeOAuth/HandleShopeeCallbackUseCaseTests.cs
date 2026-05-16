using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Ryla.Core.Ports.Outbound;
using Ryla.Core.UseCases.ShopeeOAuth;

namespace Ryla.Core.Tests.UseCases.ShopeeOAuth;

public sealed class HandleShopeeCallbackUseCaseTests
{
    private readonly IShopeeOAuthPort _oauthPort = Substitute.For<IShopeeOAuthPort>();
    private readonly IShopeeTokenPort _tokenPort = Substitute.For<IShopeeTokenPort>();
    private readonly IHandleShopeeCallbackUseCase _sut;

    private static readonly Guid TenantId = Guid.NewGuid();
    private const string Code = "auth-code-abc";
    private const long ShopId = 123456789L;

    public HandleShopeeCallbackUseCaseTests()
    {
        _sut = new HandleShopeeCallbackUseCase(
            _oauthPort,
            _tokenPort,
            NullLogger<HandleShopeeCallbackUseCase>.Instance);
    }

    [Fact]
    public async Task ExecuteAsync__WhenCodeIsValid__ShouldExchangeCodeAndSaveTokens()
    {
        // Arrange
        var expiresAt = DateTimeOffset.UtcNow.AddHours(4);
        var tokenResponse = new ShopeeTokenResponse(
            AccessToken: "access-token",
            RefreshToken: "refresh-token",
            ExpiresAt: expiresAt,
            ShopId: ShopId);

        _oauthPort.ExchangeCodeAsync(Code, ShopId, Arg.Any<CancellationToken>())
            .Returns(tokenResponse);

        // Act
        await _sut.ExecuteAsync(TenantId, Code, ShopId);

        // Assert
        await _oauthPort.Received(1).ExchangeCodeAsync(Code, ShopId, Arg.Any<CancellationToken>());
        await _tokenPort.Received(1).SaveTokenAsync(
            TenantId,
            ShopId,
            "access-token",
            "refresh-token",
            expiresAt,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync__WhenOAuthPortThrows__ShouldPropagateException()
    {
        // Arrange
        _oauthPort.ExchangeCodeAsync(Arg.Any<string>(), Arg.Any<long>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Shopee API error"));

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _sut.ExecuteAsync(TenantId, Code, ShopId));

        await _tokenPort.DidNotReceive().SaveTokenAsync(
            Arg.Any<Guid>(), Arg.Any<long>(), Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>());
    }
}
