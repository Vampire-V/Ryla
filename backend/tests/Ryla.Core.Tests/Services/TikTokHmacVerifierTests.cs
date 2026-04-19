using System.Security.Cryptography;
using System.Text;
using Ryla.Core.Services;
using Xunit;

namespace Ryla.Core.Tests.Services;

public class TikTokHmacVerifierTests
{
    private const string ClientSecret = "test-secret-key";

    // Helper: สร้าง valid signature header สำหรับ test
    private static string BuildSignatureHeader(string rawBody, long timestamp)
    {
        var payload = $"{timestamp}.{rawBody}";
        var keyBytes = Encoding.UTF8.GetBytes(ClientSecret);
        var payloadBytes = Encoding.UTF8.GetBytes(payload);
        using var hmac = new HMACSHA256(keyBytes);
        var hash = hmac.ComputeHash(payloadBytes);
        var hex = Convert.ToHexString(hash).ToLowerInvariant();
        return $"t={timestamp},s={hex}";
    }

    [Fact]
    public void VerifyCore__WhenSignatureIsValid__ShouldReturnTrue()
    {
        var rawBody = """{"event":"ORDER_STATUS_CHANGE","create_time":1000}""";
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var header = BuildSignatureHeader(rawBody, now);

        var result = TikTokHmacVerifier.VerifyCore(rawBody, header, ClientSecret, now);

        Assert.True(result);
    }

    [Fact]
    public void VerifyCore__WhenSignatureIsWrong__ShouldReturnFalse()
    {
        var rawBody = """{"event":"ORDER_STATUS_CHANGE"}""";
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var header = $"t={now},s=deadbeefdeadbeefdeadbeefdeadbeefdeadbeefdeadbeefdeadbeefdeadbeef";

        var result = TikTokHmacVerifier.VerifyCore(rawBody, header, ClientSecret, now);

        Assert.False(result);
    }

    [Fact]
    public void VerifyCore__WhenRequestIsTooOld__ShouldReturnFalse()
    {
        var rawBody = """{"event":"ORDER_STATUS_CHANGE"}""";
        var oldTimestamp = DateTimeOffset.UtcNow.AddMinutes(-10).ToUnixTimeSeconds();
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var header = BuildSignatureHeader(rawBody, oldTimestamp);

        var result = TikTokHmacVerifier.VerifyCore(rawBody, header, ClientSecret, now, maxAgeSeconds: 300);

        Assert.False(result);
    }

    [Fact]
    public void VerifyCore__WhenHeaderIsMalformed__ShouldReturnFalse()
    {
        var rawBody = """{"event":"ORDER_STATUS_CHANGE"}""";
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        var result = TikTokHmacVerifier.VerifyCore(rawBody, "invalid-header", ClientSecret, now);

        Assert.False(result);
    }

    [Fact]
    public void VerifyCore__WhenSignatureHeaderIsEmpty__ShouldReturnFalse()
    {
        var rawBody = """{"event":"ORDER_STATUS_CHANGE"}""";
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        var result = TikTokHmacVerifier.VerifyCore(rawBody, string.Empty, ClientSecret, now);

        Assert.False(result);
    }
}
