using System.Security.Cryptography;
using System.Text;
using Ryla.Infrastructure.Adapters.Shopee;

namespace Ryla.Infrastructure.Tests.Adapters.Shopee;

/// <summary>
/// Unit tests สำหรับ ShopeeApiSignature
/// ใช้ deterministic input → known hex เพื่อตรวจสอบ HMAC-SHA256 correctness
/// Reference: https://open.shopee.com/documents/v2/v2.account.api-signature
/// </summary>
public sealed class ShopeeApiSignatureTests
{
    private const string PartnerKey = "test-partner-key-32bytes-padded!";
    private const long PartnerId = 123456L;
    private const string Path = "/api/v2/auth/token/get";
    private const long Timestamp = 1700000000L;

    // ─── ComputePartnerSign ──────────────────────────────────────────────────

    [Fact]
    public void ComputePartnerSign__WithKnownInput__ShouldMatchExpectedHex()
    {
        // Arrange: base_string = partner_id + path + timestamp
        var baseString = $"{PartnerId}{Path}{Timestamp}";
        var expected = ComputeExpectedHmac(PartnerKey, baseString);

        // Act
        var actual = ShopeeApiSignature.ComputePartnerSign(PartnerId, Path, Timestamp, PartnerKey);

        // Assert
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void ComputePartnerSign__ShouldReturnLowercaseHex()
    {
        var result = ShopeeApiSignature.ComputePartnerSign(PartnerId, Path, Timestamp, PartnerKey);

        Assert.Equal(result.ToLowerInvariant(), result);
        Assert.Matches("^[0-9a-f]+$", result);
    }

    [Fact]
    public void ComputePartnerSign__DifferentTimestamps__ShouldProduceDifferentSignatures()
    {
        var sig1 = ShopeeApiSignature.ComputePartnerSign(PartnerId, Path, 1700000000L, PartnerKey);
        var sig2 = ShopeeApiSignature.ComputePartnerSign(PartnerId, Path, 1700000001L, PartnerKey);

        Assert.NotEqual(sig1, sig2);
    }

    // ─── ComputeShopSign ─────────────────────────────────────────────────────

    [Fact]
    public void ComputeShopSign__WithKnownInput__ShouldMatchExpectedHex()
    {
        // Arrange: base_string = partner_id + path + timestamp + access_token + shop_id
        const string accessToken = "test-access-token-abc123";
        const long shopId = 789012L;
        var baseString = $"{PartnerId}{Path}{Timestamp}{accessToken}{shopId}";
        var expected = ComputeExpectedHmac(PartnerKey, baseString);

        // Act
        var actual = ShopeeApiSignature.ComputeShopSign(
            PartnerId, Path, Timestamp, accessToken, shopId, PartnerKey);

        // Assert
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void ComputeShopSign__DifferentAccessTokens__ShouldProduceDifferentSignatures()
    {
        const long shopId = 789012L;
        var sig1 = ShopeeApiSignature.ComputeShopSign(PartnerId, Path, Timestamp, "token-a", shopId, PartnerKey);
        var sig2 = ShopeeApiSignature.ComputeShopSign(PartnerId, Path, Timestamp, "token-b", shopId, PartnerKey);

        Assert.NotEqual(sig1, sig2);
    }

    [Fact]
    public void ComputeShopSign__IsDistinctFromPartnerSign()
    {
        const string accessToken = "any-token";
        const long shopId = 789012L;
        var partnerSig = ShopeeApiSignature.ComputePartnerSign(PartnerId, Path, Timestamp, PartnerKey);
        var shopSig = ShopeeApiSignature.ComputeShopSign(
            PartnerId, Path, Timestamp, accessToken, shopId, PartnerKey);

        // partner base_string ต่างจาก shop base_string → signatures ต้องต่างกัน
        Assert.NotEqual(partnerSig, shopSig);
    }

    // ─── UnixTimestamp ───────────────────────────────────────────────────────

    [Fact]
    public void UnixTimestamp__ShouldReturnCurrentSecond()
    {
        var before = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var result = ShopeeApiSignature.UnixTimestamp();
        var after = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        Assert.InRange(result, before, after);
    }

    // ─── Helpers ─────────────────────────────────────────────────────────────

    private static string ComputeExpectedHmac(string key, string data)
    {
        var hash = HMACSHA256.HashData(
            Encoding.UTF8.GetBytes(key),
            Encoding.UTF8.GetBytes(data));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
