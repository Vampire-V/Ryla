using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Ryla.Infrastructure.Adapters.GoogleSheets;

namespace Ryla.Infrastructure.Tests.Adapters.GoogleSheets;

public sealed class GoogleJwtSignerTests
{
    private readonly GoogleJwtSigner _sut = new();

    [Fact]
    public void SignJwt__WhenCalled__ReturnsThreePartJwt()
    {
        // Arrange
        using var rsa = RSA.Create(2048);
        var keyFile = BuildKeyFile(rsa);
        var now = DateTimeOffset.UtcNow;

        // Act
        var jwt = _sut.SignJwt(keyFile, "https://www.googleapis.com/auth/spreadsheets",
            "https://oauth2.googleapis.com/token", now);

        // Assert
        var parts = jwt.Split('.');
        Assert.Equal(3, parts.Length);
    }

    [Fact]
    public void SignJwt__Header__ShouldContainRS256AndJwt()
    {
        // Arrange
        using var rsa = RSA.Create(2048);
        var keyFile = BuildKeyFile(rsa);

        // Act
        var jwt = _sut.SignJwt(keyFile, "https://www.googleapis.com/auth/spreadsheets",
            "https://oauth2.googleapis.com/token", DateTimeOffset.UtcNow);

        // Assert
        var headerJson = DecodeBase64Url(jwt.Split('.')[0]);
        using var doc = JsonDocument.Parse(headerJson);
        Assert.Equal("RS256", doc.RootElement.GetProperty("alg").GetString());
        Assert.Equal("JWT", doc.RootElement.GetProperty("typ").GetString());
    }

    [Fact]
    public void SignJwt__Payload__ShouldContainCorrectClaims()
    {
        // Arrange
        using var rsa = RSA.Create(2048);
        var keyFile = BuildKeyFile(rsa);
        var now = new DateTimeOffset(2024, 1, 1, 12, 0, 0, TimeSpan.Zero);
        const string scope = "https://www.googleapis.com/auth/spreadsheets";
        const string audience = "https://oauth2.googleapis.com/token";

        // Act
        var jwt = _sut.SignJwt(keyFile, scope, audience, now);

        // Assert
        var payloadJson = DecodeBase64Url(jwt.Split('.')[1]);
        using var doc = JsonDocument.Parse(payloadJson);
        Assert.Equal("test@project.iam.gserviceaccount.com", doc.RootElement.GetProperty("iss").GetString());
        Assert.Equal(scope, doc.RootElement.GetProperty("scope").GetString());
        Assert.Equal(audience, doc.RootElement.GetProperty("aud").GetString());
        var iat = doc.RootElement.GetProperty("iat").GetInt64();
        var exp = doc.RootElement.GetProperty("exp").GetInt64();
        Assert.Equal(now.ToUnixTimeSeconds(), iat);
        Assert.Equal(iat + 3600, exp);
    }

    [Fact]
    public void SignJwt__Signature__ShouldBeVerifiableWithPublicKey()
    {
        // Arrange
        using var rsa = RSA.Create(2048);
        var keyFile = BuildKeyFile(rsa);

        // Act
        var jwt = _sut.SignJwt(keyFile, "https://www.googleapis.com/auth/spreadsheets",
            "https://oauth2.googleapis.com/token", DateTimeOffset.UtcNow);

        // Assert: re-verify signature using the test RSA public key
        var parts = jwt.Split('.');
        var headerPayload = Encoding.UTF8.GetBytes(parts[0] + "." + parts[1]);
        var signature = Base64UrlDecode(parts[2]);

        var isValid = rsa.VerifyData(headerPayload, signature,
            HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        Assert.True(isValid);
    }

    // ─── Helpers ──────────────────────────────────────────────────────────────

    private static ServiceAccountKeyFile BuildKeyFile(RSA rsa)
    {
        var pem = rsa.ExportRSAPrivateKeyPem();
        return new ServiceAccountKeyFile(
            ClientEmail: "test@project.iam.gserviceaccount.com",
            PrivateKey: pem,
            TokenUri: "https://oauth2.googleapis.com/token");
    }

    private static string DecodeBase64Url(string base64Url)
    {
        var bytes = Base64UrlDecode(base64Url);
        return Encoding.UTF8.GetString(bytes);
    }

    private static byte[] Base64UrlDecode(string base64Url)
    {
        var s = base64Url.Replace('-', '+').Replace('_', '/');
        switch (s.Length % 4)
        {
            case 2: s += "=="; break;
            case 3: s += "="; break;
        }
        return Convert.FromBase64String(s);
    }
}
