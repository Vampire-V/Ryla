using Microsoft.Extensions.Options;
using NSubstitute;
using Ryla.Core.Configuration;
using Ryla.Infrastructure.Adapters.Shopee;

namespace Ryla.Infrastructure.Tests.Adapters.Shopee;

/// <summary>
/// Unit tests สำหรับ ShopeeOAuthAdapter
/// ทดสอบเฉพาะ pure computation methods ที่ไม่ต้องการ HTTP
/// </summary>
public sealed class ShopeeOAuthAdapterTests
{
    private readonly ShopeeOptions _options = new()
    {
        PartnerId = 12345L,
        PartnerKey = "test-partner-key-abc123xyz",
        ApiBaseUrl = "https://partner.shopeemobile.com",
    };

    private ShopeeOAuthAdapter CreateSut()
    {
        var optionsMock = Substitute.For<IOptions<ShopeeOptions>>();
        optionsMock.Value.Returns(_options);
        // HttpClient ไม่ถูกเรียกใน GenerateAuthorizationUrlAsync
        return new ShopeeOAuthAdapter(new HttpClient(), optionsMock);
    }

    // ─── GenerateAuthorizationUrlAsync ───────────────────────────────────────

    [Fact]
    public async Task GenerateAuthorizationUrlAsync__ShouldReturnUrlWithRequiredQueryParams()
    {
        var sut = CreateSut();
        var redirectUrl = "https://app.ryla.io/callback";

        var result = await sut.GenerateAuthorizationUrlAsync(redirectUrl);

        Assert.Contains($"partner_id={_options.PartnerId}", result);
        Assert.Contains("timestamp=", result);
        Assert.Contains("sign=", result);
        Assert.Contains(Uri.EscapeDataString(redirectUrl), result);
    }

    [Fact]
    public async Task GenerateAuthorizationUrlAsync__ShouldUseConfiguredApiBaseUrl()
    {
        var sut = CreateSut();

        var result = await sut.GenerateAuthorizationUrlAsync("https://example.com/cb");

        Assert.StartsWith(_options.ApiBaseUrl, result);
    }

    [Fact]
    public async Task GenerateAuthorizationUrlAsync__SignShouldBeLowercaseHex()
    {
        var sut = CreateSut();

        var result = await sut.GenerateAuthorizationUrlAsync("https://example.com/cb");

        // parse sign= value
        var signStart = result.IndexOf("sign=", StringComparison.Ordinal) + 5;
        var signEnd = result.IndexOf('&', signStart);
        var sign = signEnd < 0 ? result[signStart..] : result[signStart..signEnd];

        Assert.Matches("^[0-9a-f]+$", sign);
        Assert.Equal(64, sign.Length); // SHA-256 = 32 bytes = 64 hex chars
    }

    [Fact]
    public async Task GenerateAuthorizationUrlAsync__DifferentRedirects__ShouldProduceDifferentUrls()
    {
        var sut = CreateSut();

        var url1 = await sut.GenerateAuthorizationUrlAsync("https://app1.example.com/cb");
        var url2 = await sut.GenerateAuthorizationUrlAsync("https://app2.example.com/cb");

        Assert.NotEqual(url1, url2);
    }
}
