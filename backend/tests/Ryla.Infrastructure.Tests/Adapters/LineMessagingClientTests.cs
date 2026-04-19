using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Ryla.Core.Configuration;
using Ryla.Infrastructure.Adapters.LineMessaging;

namespace Ryla.Infrastructure.Tests.Adapters;

public sealed class LineMessagingClientTests
{
    private static LineMessagingClient CreateClient(
        HttpMessageHandler handler,
        string baseUrl = "https://api.line.me")
    {
        var httpClient = new HttpClient(handler);
        var options = Options.Create(new LineOptions { BaseUrl = baseUrl });
        return new LineMessagingClient(
            httpClient,
            options,
            NullLogger<LineMessagingClient>.Instance);
    }

    [Fact]
    public async Task PushAsync__When2xxResponse__ReturnsSuccess()
    {
        // Arrange
        var handler = new FakeHttpHandler(HttpStatusCode.OK, "{}");
        var client = CreateClient(handler);

        // Act
        var result = await client.PushAsync("tok", "uid", "hello");

        // Assert
        Assert.True(result.Success);
        Assert.Null(result.ErrorCode);
    }

    [Fact]
    public async Task PushAsync__When401__ReturnsFailureWithHttp401()
    {
        // Arrange
        var handler = new FakeHttpHandler(
            HttpStatusCode.Unauthorized,
            """{"message":"The request body has 1 error(s)","details":[]}""");
        var client = CreateClient(handler);

        // Act
        var result = await client.PushAsync("bad-token", "uid", "hello");

        // Assert
        Assert.False(result.Success);
        Assert.Equal("http_401", result.ErrorCode);
    }

    [Fact]
    public async Task PushAsync__WhenNetworkTimeout__ReturnsFailureWithNetworkError()
    {
        // Arrange
        var handler = new ThrowingHttpHandler(new TaskCanceledException("timeout"));
        var client = CreateClient(handler);

        // Act
        var result = await client.PushAsync("tok", "uid", "hello");

        // Assert
        Assert.False(result.Success);
        Assert.Equal("network_error", result.ErrorCode);
    }

    // ─── Fake handlers ────────────────────────────────────────────────────────

    private sealed class FakeHttpHandler(HttpStatusCode statusCode, string body)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json")
            };
            return Task.FromResult(response);
        }
    }

    private sealed class ThrowingHttpHandler(Exception exception) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromException<HttpResponseMessage>(exception);
    }
}
