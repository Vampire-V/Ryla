using System.Text.Json;
using System.Text.Json.Serialization;

namespace Ryla.Core.Domain.Webhooks;

/// <summary>
/// Shopee Push Notification payload
/// Doc: https://open.shopee.com → Push Mechanism
/// </summary>
public sealed record ShopeeWebhookPayload
{
    /// <summary>Event type code (3 = order status change)</summary>
    [JsonPropertyName("code")]
    public int Code { get; init; }

    /// <summary>Shop ID ที่เกิด event</summary>
    [JsonPropertyName("shop_id")]
    public long ShopId { get; init; }

    /// <summary>Unix timestamp (seconds) ตอนที่ event เกิด</summary>
    [JsonPropertyName("timestamp")]
    public long Timestamp { get; init; }

    /// <summary>
    /// Event-specific data — ใช้ JsonElement เพราะ schema ต่างกันตาม code
    /// สำหรับ code 3: มี ordersn, status, update_time
    /// </summary>
    [JsonPropertyName("data")]
    public JsonElement Data { get; init; }
}
