using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Ryla.Infrastructure.Adapters.GoogleSheets;

/// <summary>
/// Singleton service สร้าง JWT สำหรับ Google OAuth2 service account
/// ใช้ RSA.ImportFromPem + SignData โดยตรง — ไม่มี third-party JWT library
/// AOT-safe: ไม่มี reflection หรือ dynamic code
/// </summary>
internal sealed class GoogleJwtSigner
{
    private static readonly byte[] HeaderBytes = BuildHeader();

    /// <summary>
    /// สร้าง JWT string ในรูปแบบ header.payload.signature (base64url-encoded)
    /// </summary>
    public string SignJwt(
        ServiceAccountKeyFile key,
        string scope,
        string audience,
        DateTimeOffset now)
    {
        var iat = now.ToUnixTimeSeconds();
        var exp = iat + 3600;

        var payload = BuildPayload(key.ClientEmail, scope, audience, iat, exp);
        var headerB64 = Base64UrlEncode(HeaderBytes);
        var payloadB64 = Base64UrlEncode(payload);

        var signingInput = Encoding.UTF8.GetBytes($"{headerB64}.{payloadB64}");
        var signature = SignWithRsa(key.PrivateKey, signingInput);

        return $"{headerB64}.{payloadB64}.{Base64UrlEncode(signature)}";
    }

    private static byte[] BuildHeader()
    {
        var header = """{"alg":"RS256","typ":"JWT"}""";
        return Encoding.UTF8.GetBytes(header);
    }

    private static byte[] BuildPayload(
        string iss, string scope, string aud, long iat, long exp)
    {
        // ใช้ anonymous type + JsonSerializer เพราะ payload เป็น one-off struct ไม่ต้องการ record
        var payloadObj = new JwtPayload(iss, scope, aud, exp, iat);
        var json = JsonSerializer.Serialize(payloadObj, GoogleSheetsJsonContext.Default.JwtPayload);
        return Encoding.UTF8.GetBytes(json);
    }

    private static byte[] SignWithRsa(string privateKeyPem, byte[] data)
    {
        using var rsa = RSA.Create();
        rsa.ImportFromPem(privateKeyPem);
        return rsa.SignData(data, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
    }

    private static string Base64UrlEncode(byte[] input)
    {
        return Convert.ToBase64String(input)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }
}
