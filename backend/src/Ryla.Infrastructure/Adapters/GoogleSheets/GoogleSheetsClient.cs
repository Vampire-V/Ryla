using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Ryla.Core.Configuration;
using Ryla.Core.Domain.Integrations;
using Ryla.Core.Ports.Outbound;

namespace Ryla.Infrastructure.Adapters.GoogleSheets;

/// <summary>
/// Google Sheets API adapter — appends rows via Sheets API v4
/// อ่าน service account JSON จาก credentials, สร้าง JWT, แลก OAuth2 token, แล้ว POST append
/// Token cache อยู่ใน singleton GoogleSheetsTokenCache — typed HttpClient เป็น transient
/// AOT-safe: ไม่มี reflection, ใช้ HttpClient + STJ source-gen
/// </summary>
internal sealed class GoogleSheetsClient(
    HttpClient http,
    IOptions<GoogleSheetsOptions> opts,
    GoogleJwtSigner signer,
    GoogleSheetsTokenCache tokenCache,
    ILogger<GoogleSheetsClient> logger) : ISheetAppender
{
    private const string Scope = "https://www.googleapis.com/auth/spreadsheets";

    public async Task<SheetAppendResult> AppendRowAsync(
        GoogleSheetsCredentials creds,
        IReadOnlyList<string> values,
        CancellationToken ct = default)
    {
        ServiceAccountKeyFile keyFile;
        try
        {
            keyFile = ParseServiceAccount(creds.ServiceAccountJson);
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException)
        {
            logger.LogWarning(ex, "Google Sheets: failed to parse service_account_json");
            return new SheetAppendResult(false, "bad_credentials", ex.Message);
        }

        string token;
        try
        {
            token = await GetOrRefreshTokenAsync(keyFile, ct);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            logger.LogWarning(ex, "Google Sheets: token request failed");
            return MapException(ex);
        }

        return await AppendToSheetAsync(creds, values, token, ct);
    }

    private async Task<string> GetOrRefreshTokenAsync(
        ServiceAccountKeyFile keyFile,
        CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;

        if (tokenCache.TryGet(keyFile.ClientEmail, now, TimeSpan.FromSeconds(60), out var cached))
        {
            return cached;
        }

        var jwt = signer.SignJwt(keyFile, Scope, opts.Value.TokenEndpoint, now);
        var formContent = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "urn:ietf:params:oauth:grant-type:jwt-bearer",
            ["assertion"] = jwt
        });

        using var response = await http.PostAsync(opts.Value.TokenEndpoint, formContent, ct);
        var body = await response.Content.ReadAsStringAsync(ct);
        var tokenResponse = JsonSerializer.Deserialize(body, GoogleSheetsJsonContext.Default.OAuthTokenResponse);

        var accessToken = tokenResponse?.AccessToken
            ?? throw new InvalidOperationException("Token response missing access_token");

        var expiresAt = now.AddSeconds(tokenResponse.ExpiresIn);
        tokenCache.Store(keyFile.ClientEmail, accessToken, expiresAt);

        return accessToken;
    }

    private async Task<SheetAppendResult> AppendToSheetAsync(
        GoogleSheetsCredentials creds,
        IReadOnlyList<string> values,
        string token,
        CancellationToken ct)
    {
        var url = BuildAppendUrl(creds);
        var requestBody = new AppendValuesRequest(
            MajorDimension: "ROWS",
            Values: [values]);

        var json = JsonSerializer.Serialize(requestBody, GoogleSheetsJsonContext.Default.AppendValuesRequest);
        using var content = new StringContent(json, Encoding.UTF8, "application/json");
        using var request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Content = content;

        try
        {
            using var response = await http.SendAsync(request, ct);
            if (response.IsSuccessStatusCode)
                return new SheetAppendResult(true);

            return await HandleErrorResponseAsync(response, ct);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            logger.LogWarning(ex, "Google Sheets: append request failed");
            return MapException(ex);
        }
    }

    private async Task<SheetAppendResult> HandleErrorResponseAsync(
        HttpResponseMessage response,
        CancellationToken ct)
    {
        var statusCode = (int)response.StatusCode;
        var errorCode = MapStatusCode(statusCode);

        try
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            var errorResponse = JsonSerializer.Deserialize(body, GoogleSheetsJsonContext.Default.GoogleApiErrorResponse);
            var message = errorResponse?.Error?.Message ?? body;
            logger.LogWarning("Google Sheets API returned {StatusCode}: {Message}", statusCode, message);
            return new SheetAppendResult(false, errorCode, message);
        }
        catch (JsonException)
        {
            logger.LogWarning("Google Sheets API returned {StatusCode} with non-JSON body", statusCode);
            return new SheetAppendResult(false, errorCode, $"HTTP {statusCode}");
        }
    }

    private static ServiceAccountKeyFile ParseServiceAccount(string json)
    {
        var keyFile = JsonSerializer.Deserialize(json, GoogleSheetsJsonContext.Default.ServiceAccountKeyFile);
        if (keyFile is null || string.IsNullOrEmpty(keyFile.ClientEmail) || string.IsNullOrEmpty(keyFile.PrivateKey))
            throw new InvalidOperationException("Service account JSON is missing required fields");
        return keyFile;
    }

    private string BuildAppendUrl(GoogleSheetsCredentials creds)
    {
        var range = Uri.EscapeDataString($"{creds.WorksheetName}!A:Z");
        return $"{opts.Value.BaseUrl}/v4/spreadsheets/{creds.SpreadsheetId}/values/{range}:append" +
               "?valueInputOption=USER_ENTERED&insertDataOption=INSERT_ROWS";
    }

    private static string MapStatusCode(int statusCode) => statusCode switch
    {
        401 => "unauthorized",
        403 => "forbidden",
        404 => "not_found",
        429 => "rate_limited",
        >= 500 => "upstream_error",
        _ => $"http_{statusCode}"
    };

    private static SheetAppendResult MapException(Exception ex) => ex switch
    {
        TaskCanceledException => new SheetAppendResult(false, "timeout", ex.Message),
        _ => new SheetAppendResult(false, "network_error", ex.Message)
    };
}
