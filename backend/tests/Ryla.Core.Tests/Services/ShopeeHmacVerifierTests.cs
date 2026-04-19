using System.Security.Cryptography;
using System.Text;
using Ryla.Core.Services;
using Xunit;

namespace Ryla.Core.Tests.Services;

public class ShopeeHmacVerifierTests
{
    private const string PartnerKey = "test-partner-key";
    private const string CallbackUrl = "https://example.com/webhooks/shopee";

    private static string ComputeSignature(string rawBody)
    {
        var baseString = CallbackUrl + rawBody;
        var keyBytes = Encoding.UTF8.GetBytes(PartnerKey);
        var dataBytes = Encoding.UTF8.GetBytes(baseString);
        using var hmac = new HMACSHA256(keyBytes);
        var hash = hmac.ComputeHash(dataBytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    [Fact]
    public void VerifyCore__WhenSignatureIsValid__ShouldReturnTrue()
    {
        var rawBody = """{"code":3,"shop_id":123,"timestamp":1000}""";
        var signature = ComputeSignature(rawBody);
        var result = ShopeeHmacVerifier.VerifyCore(rawBody, signature, PartnerKey, CallbackUrl);
        Assert.True(result);
    }

    [Fact]
    public void VerifyCore__WhenSignatureIsWrong__ShouldReturnFalse()
    {
        var rawBody = """{"code":3,"shop_id":123,"timestamp":1000}""";
        var signature = "deadbeefdeadbeefdeadbeefdeadbeefdeadbeefdeadbeefdeadbeefdeadbeef";
        var result = ShopeeHmacVerifier.VerifyCore(rawBody, signature, PartnerKey, CallbackUrl);
        Assert.False(result);
    }

    [Fact]
    public void VerifyCore__WhenSignatureIsEmpty__ShouldReturnFalse()
    {
        var rawBody = """{"code":3,"shop_id":123,"timestamp":1000}""";
        var result = ShopeeHmacVerifier.VerifyCore(rawBody, string.Empty, PartnerKey, CallbackUrl);
        Assert.False(result);
    }

    [Fact]
    public void VerifyCore__WhenSignatureIsNull__ShouldReturnFalse()
    {
        var rawBody = """{"code":3,"shop_id":123,"timestamp":1000}""";
        var result = ShopeeHmacVerifier.VerifyCore(rawBody, null!, PartnerKey, CallbackUrl);
        Assert.False(result);
    }

    [Fact]
    public void VerifyCore__WhenBodyIsEmpty__ShouldReturnFalse()
    {
        var signature = ComputeSignature("");
        var result = ShopeeHmacVerifier.VerifyCore(string.Empty, signature, PartnerKey, CallbackUrl);
        Assert.False(result);
    }

    [Fact]
    public void VerifyCore__WhenCallbackUrlDiffers__ShouldReturnFalse()
    {
        var rawBody = """{"code":3,"shop_id":123,"timestamp":1000}""";
        var signature = ComputeSignature(rawBody);
        var result = ShopeeHmacVerifier.VerifyCore(rawBody, signature, PartnerKey, "https://wrong-domain.com/webhooks/shopee");
        Assert.False(result);
    }
}
