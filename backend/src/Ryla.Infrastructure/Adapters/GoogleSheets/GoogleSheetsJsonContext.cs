using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace Ryla.Infrastructure.Adapters.GoogleSheets;

/// <summary>
/// Source-generated JSON context สำหรับ Google Sheets API
/// AOT-safe: ไม่มี reflection, ใช้ source generation เท่านั้น
/// snake_case Google API fields ใช้ [JsonPropertyName] override แทน global policy
/// </summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(AppendValuesRequest))]
[JsonSerializable(typeof(AppendValuesResponse))]
[JsonSerializable(typeof(OAuthTokenResponse))]
[JsonSerializable(typeof(GoogleApiErrorResponse))]
[JsonSerializable(typeof(GoogleApiErrorBody))]
[JsonSerializable(typeof(ServiceAccountKeyFile))]
[JsonSerializable(typeof(JwtPayload))]
[ExcludeFromCodeCoverage]
internal sealed partial class GoogleSheetsJsonContext : JsonSerializerContext;

[ExcludeFromCodeCoverage]
internal sealed record AppendValuesRequest(
    string MajorDimension,
    IReadOnlyList<IReadOnlyList<string>> Values);

[ExcludeFromCodeCoverage]
internal sealed record AppendValuesResponse(
    string? SpreadsheetId,
    AppendUpdatesResponse? Updates);

[ExcludeFromCodeCoverage]
internal sealed record AppendUpdatesResponse(
    string? UpdatedRange,
    int UpdatedRows);

[ExcludeFromCodeCoverage]
internal sealed record OAuthTokenResponse(
    [property: JsonPropertyName("access_token")] string? AccessToken,
    [property: JsonPropertyName("expires_in")] int ExpiresIn,
    [property: JsonPropertyName("token_type")] string? TokenType);

[ExcludeFromCodeCoverage]
internal sealed record GoogleApiErrorResponse(
    GoogleApiErrorBody? Error);

[ExcludeFromCodeCoverage]
internal sealed record GoogleApiErrorBody(
    int Code,
    string? Message,
    string? Status);

[ExcludeFromCodeCoverage]
internal sealed record ServiceAccountKeyFile(
    [property: JsonPropertyName("client_email")] string ClientEmail,
    [property: JsonPropertyName("private_key")] string PrivateKey,
    [property: JsonPropertyName("token_uri")] string TokenUri);

[ExcludeFromCodeCoverage]
internal sealed record JwtPayload(
    [property: JsonPropertyName("iss")] string Iss,
    [property: JsonPropertyName("scope")] string Scope,
    [property: JsonPropertyName("aud")] string Aud,
    [property: JsonPropertyName("exp")] long Exp,
    [property: JsonPropertyName("iat")] long Iat);
