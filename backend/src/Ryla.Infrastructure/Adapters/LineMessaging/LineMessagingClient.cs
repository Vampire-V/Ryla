using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Ryla.Core.Configuration;
using Ryla.Core.Ports.Outbound;

namespace Ryla.Infrastructure.Adapters.LineMessaging;

/// <summary>
/// LINE Messaging API adapter
/// POST /v2/bot/message/push พร้อม Bearer token ต่อ request
/// </summary>
internal sealed class LineMessagingClient(
    HttpClient httpClient,
    IOptions<LineOptions> options,
    ILogger<LineMessagingClient> logger) : ILineNotifier
{
    private const string PushEndpointPath = "/v2/bot/message/push";

    public async Task<LineNotifyResult> PushAsync(
        string channelAccessToken,
        string targetUserId,
        string message,
        CancellationToken ct = default)
    {
        var requestBody = new LinePushRequest(
            To: targetUserId,
            Messages: [new LineMessage("text", message)]);

        var json = JsonSerializer.Serialize(requestBody, LineJsonContext.Default.LinePushRequest);
        using var content = new StringContent(json, Encoding.UTF8, "application/json");

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            options.Value.BaseUrl + PushEndpointPath);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", channelAccessToken);
        request.Content = content;

        return await SendRequestAsync(request, ct);
    }

    private async Task<LineNotifyResult> SendRequestAsync(
        HttpRequestMessage request,
        CancellationToken ct)
    {
        try
        {
            using var response = await httpClient.SendAsync(request, ct);

            if (response.IsSuccessStatusCode)
                return new LineNotifyResult(true);

            return await HandleErrorResponseAsync(response, ct);
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning(ex, "LINE API: network error");
            return new LineNotifyResult(false, "network_error", ex.Message);
        }
        catch (TaskCanceledException ex)
        {
            logger.LogWarning(ex, "LINE API: request timeout");
            return new LineNotifyResult(false, "network_error", ex.Message);
        }
    }

    private async Task<LineNotifyResult> HandleErrorResponseAsync(
        HttpResponseMessage response,
        CancellationToken ct)
    {
        var statusCode = (int)response.StatusCode;
        var errorCode = $"http_{statusCode}";

        try
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            var errorResponse = JsonSerializer.Deserialize(body, LineJsonContext.Default.LineErrorResponse);
            var errorMessage = errorResponse?.Message ?? body;

            logger.LogWarning(
                "LINE API returned {StatusCode}: {ErrorMessage}",
                statusCode, errorMessage);
            return new LineNotifyResult(false, errorCode, errorMessage);
        }
        catch (JsonException)
        {
            logger.LogWarning("LINE API returned {StatusCode} with non-JSON body", statusCode);
            return new LineNotifyResult(false, errorCode, $"HTTP {statusCode}");
        }
    }
}
