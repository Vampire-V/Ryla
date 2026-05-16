using NSubstitute;
using Ryla.Core.Ports.Outbound;
using Ryla.Core.UseCases.ShopeeOAuth;

namespace Ryla.Core.Tests.UseCases.ShopeeOAuth;

public sealed class AuthorizeShopeeUseCaseTests
{
    private readonly IShopeeOAuthPort _oauthPort = Substitute.For<IShopeeOAuthPort>();
    private readonly IAuthorizeShopeeUseCase _sut;

    public AuthorizeShopeeUseCaseTests()
    {
        _sut = new AuthorizeShopeeUseCase(_oauthPort);
    }

    [Fact]
    public async Task ExecuteAsync__WhenCalled__ShouldReturnUrlFromOAuthPort()
    {
        // Arrange
        const string redirectUrl = "https://app.ryla.io/oauth/shopee/callback";
        const string expectedAuthUrl = "https://partner.shopeemobile.com/api/v2/shop/auth_partner?redirect_url=...";

        _oauthPort.GenerateAuthorizationUrlAsync(redirectUrl, Arg.Any<CancellationToken>())
            .Returns(expectedAuthUrl);

        // Act
        var result = await _sut.ExecuteAsync(redirectUrl);

        // Assert
        Assert.Equal(expectedAuthUrl, result);
        await _oauthPort.Received(1).GenerateAuthorizationUrlAsync(redirectUrl, Arg.Any<CancellationToken>());
    }
}
