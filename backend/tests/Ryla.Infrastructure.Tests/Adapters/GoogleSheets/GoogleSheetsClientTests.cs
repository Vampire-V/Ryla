using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Ryla.Core.Configuration;
using Ryla.Core.Domain.Integrations;
using Ryla.Infrastructure.Adapters.GoogleSheets;

namespace Ryla.Infrastructure.Tests.Adapters.GoogleSheets;

public sealed class GoogleSheetsClientTests
{
    private static readonly GoogleSheetsCredentials ValidCreds;
    private static readonly RSA TestRsa = RSA.Create(2048);

    static GoogleSheetsClientTests()
    {
        var pem = TestRsa.ExportRSAPrivateKeyPem();
        var keyJson = JsonSerializer.Serialize(new
        {
            client_email = "test@project.iam.gserviceaccount.com",
            private_key = pem,
            token_uri = "https://oauth2.googleapis.com/token"
        });
        ValidCreds = new GoogleSheetsCredentials("spreadsheet-id-123", "Sheet1", keyJson);
    }

    private static GoogleSheetsClient CreateClient(
        HttpMessageHandler handler,
        string baseUrl = "https://sheets.googleapis.com",
        string tokenEndpoint = "https://oauth2.googleapis.com/token")
    {
        var httpClient = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(5) };
        var options = Options.Create(new GoogleSheetsOptions
        {
            BaseUrl = baseUrl,
            TokenEndpoint = tokenEndpoint
        });
        var signer = new GoogleJwtSigner();
        var tokenCache = new GoogleSheetsTokenCache();
        return new GoogleSheetsClient(
            httpClient,
            options,
            signer,
            tokenCache,
            NullLogger<GoogleSheetsClient>.Instance);
    }

    private static string TokenResponse() =>
        """{"access_token":"test-access-token","expires_in":3600,"token_type":"Bearer"}""";

    [Fact]
    public async Task AppendRowAsync__When200Response__ReturnsSuccess()
    {
        // Arrange: first call = token, second call = append
        var handler = new SequentialFakeHandler(
            new FakeResponse(HttpStatusCode.OK, TokenResponse()),
            new FakeResponse(HttpStatusCode.OK, """{"spreadsheetId":"abc","updates":{}}"""));
        var client = CreateClient(handler);

        // Act
        var result = await client.AppendRowAsync(ValidCreds, ["2024-01-01", "TikTok", "ORD-1", "CONFIRMED"]);

        // Assert
        Assert.True(result.Success);
        Assert.Null(result.ErrorCode);
    }

    [Fact]
    public async Task AppendRowAsync__WhenTokenReturned__BearerHeaderIsSet()
    {
        // Arrange
        HttpRequestMessage? capturedAppend = null;
        var handler = new CapturingSequentialHandler(
            tokenResponse: new FakeResponse(HttpStatusCode.OK, TokenResponse()),
            appendResponse: new FakeResponse(HttpStatusCode.OK, """{"spreadsheetId":"abc","updates":{}}"""),
            onAppend: req => capturedAppend = req);
        var client = CreateClient(handler);

        // Act
        await client.AppendRowAsync(ValidCreds, ["v1", "v2"]);

        // Assert
        Assert.NotNull(capturedAppend);
        Assert.Equal("test-access-token", capturedAppend!.Headers.Authorization?.Parameter);
        Assert.Equal("Bearer", capturedAppend.Headers.Authorization?.Scheme);
    }

    [Fact]
    public async Task AppendRowAsync__WhenCalledTwice__ShouldReuseToken()
    {
        // Arrange: 1 token request + 2 append requests
        var handler = new SequentialFakeHandler(
            new FakeResponse(HttpStatusCode.OK, TokenResponse()),
            new FakeResponse(HttpStatusCode.OK, """{"spreadsheetId":"abc","updates":{}}"""),
            new FakeResponse(HttpStatusCode.OK, """{"spreadsheetId":"abc","updates":{}}"""));
        var client = CreateClient(handler);

        // Act
        await client.AppendRowAsync(ValidCreds, ["v1"]);
        await client.AppendRowAsync(ValidCreds, ["v2"]);

        // Assert: only 1 token request (index 0), then 2 appends — handler confirms order
        Assert.Equal(3, handler.CallCount);
    }

    [Fact]
    public async Task AppendRowAsync__When401__ReturnsUnauthorized()
    {
        // Arrange
        var handler = new SequentialFakeHandler(
            new FakeResponse(HttpStatusCode.OK, TokenResponse()),
            new FakeResponse(HttpStatusCode.Unauthorized, """{"error":{"code":401,"message":"Invalid credentials"}}"""));
        var client = CreateClient(handler);

        // Act
        var result = await client.AppendRowAsync(ValidCreds, ["v1"]);

        // Assert
        Assert.False(result.Success);
        Assert.Equal("unauthorized", result.ErrorCode);
    }

    [Fact]
    public async Task AppendRowAsync__When403__ReturnsForbidden()
    {
        // Arrange
        var handler = new SequentialFakeHandler(
            new FakeResponse(HttpStatusCode.OK, TokenResponse()),
            new FakeResponse(HttpStatusCode.Forbidden, """{"error":{"code":403,"message":"Forbidden"}}"""));
        var client = CreateClient(handler);

        // Act
        var result = await client.AppendRowAsync(ValidCreds, ["v1"]);

        // Assert
        Assert.False(result.Success);
        Assert.Equal("forbidden", result.ErrorCode);
    }

    [Fact]
    public async Task AppendRowAsync__When429__ReturnsRateLimited()
    {
        // Arrange
        var handler = new SequentialFakeHandler(
            new FakeResponse(HttpStatusCode.OK, TokenResponse()),
            new FakeResponse(HttpStatusCode.TooManyRequests, """{"error":{"code":429,"message":"Rate Limit"}}"""));
        var client = CreateClient(handler);

        // Act
        var result = await client.AppendRowAsync(ValidCreds, ["v1"]);

        // Assert
        Assert.False(result.Success);
        Assert.Equal("rate_limited", result.ErrorCode);
    }

    [Fact]
    public async Task AppendRowAsync__When500__ReturnsUpstreamError()
    {
        // Arrange
        var handler = new SequentialFakeHandler(
            new FakeResponse(HttpStatusCode.OK, TokenResponse()),
            new FakeResponse(HttpStatusCode.InternalServerError, """{"error":{"code":500,"message":"Internal error"}}"""));
        var client = CreateClient(handler);

        // Act
        var result = await client.AppendRowAsync(ValidCreds, ["v1"]);

        // Assert
        Assert.False(result.Success);
        Assert.Equal("upstream_error", result.ErrorCode);
    }

    [Fact]
    public async Task AppendRowAsync__WhenTimeout__ReturnsTimeout()
    {
        // Arrange: token OK, then append times out
        var handler = new SequentialFakeHandler(
            new FakeResponse(HttpStatusCode.OK, TokenResponse()),
            new FakeResponse(HttpStatusCode.OK, "", throwException: new TaskCanceledException("timeout")));
        var client = CreateClient(handler);

        // Act
        var result = await client.AppendRowAsync(ValidCreds, ["v1"]);

        // Assert
        Assert.False(result.Success);
        Assert.Equal("timeout", result.ErrorCode);
    }

    [Fact]
    public async Task AppendRowAsync__WhenServiceAccountJsonIsInvalid__ReturnsBadCredentials()
    {
        // Arrange: invalid service_account_json
        var badCreds = new GoogleSheetsCredentials("id", "Sheet1", "NOT_JSON");
        var handler = new SequentialFakeHandler(); // should not be called at all
        var client = CreateClient(handler);

        // Act
        var result = await client.AppendRowAsync(badCreds, ["v1"]);

        // Assert
        Assert.False(result.Success);
        Assert.Equal("bad_credentials", result.ErrorCode);
    }

    // ─── Test doubles ─────────────────────────────────────────────────────────

    private sealed record FakeResponse(
        HttpStatusCode StatusCode,
        string Body,
        Exception? throwException = null);

    private sealed class SequentialFakeHandler(params FakeResponse[] responses) : HttpMessageHandler
    {
        private int _index;
        public int CallCount => _index;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (_index >= responses.Length)
                throw new InvalidOperationException(
                    $"Unexpected call #{_index + 1} — only {responses.Length} responses configured.");

            var resp = responses[_index++];
            if (resp.throwException is not null)
                return Task.FromException<HttpResponseMessage>(resp.throwException);

            return Task.FromResult(new HttpResponseMessage(resp.StatusCode)
            {
                Content = new StringContent(resp.Body, Encoding.UTF8, "application/json")
            });
        }
    }

    private sealed class CapturingSequentialHandler(
        FakeResponse tokenResponse,
        FakeResponse appendResponse,
        Action<HttpRequestMessage> onAppend) : HttpMessageHandler
    {
        private int _index;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var resp = _index++ == 0 ? tokenResponse : appendResponse;
            if (_index > 1) onAppend(request);

            return Task.FromResult(new HttpResponseMessage(resp.StatusCode)
            {
                Content = new StringContent(resp.Body, Encoding.UTF8, "application/json")
            });
        }
    }
}
