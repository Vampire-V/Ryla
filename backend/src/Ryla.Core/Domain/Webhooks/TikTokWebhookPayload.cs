using System.Text.Json.Serialization;

namespace Ryla.Core.Domain.Webhooks;

/// <summary>
/// TikTok Shop webhook payload
/// Doc: https://partner.tiktokshop.com/docv2/page/tts-webhooks-overview
/// </summary>
public sealed record TikTokWebhookPayload
{
    /// <summary>Partner app client key</summary>
    [JsonPropertyName("client_key")]
    public string ClientKey { get; init; } = string.Empty;

    /// <summary>Event type เช่น ORDER_STATUS_CHANGE</summary>
    [JsonPropertyName("event")]
    public string Event { get; init; } = string.Empty;

    /// <summary>Unix timestamp (seconds) ตอนที่ event เกิด</summary>
    [JsonPropertyName("create_time")]
    public long CreateTime { get; init; }

    /// <summary>
    /// Serialized JSON string ที่มี event-specific data
    /// ต้อง deserialize แยกตาม Event type
    /// </summary>
    [JsonPropertyName("content")]
    public string Content { get; init; } = string.Empty;
}
