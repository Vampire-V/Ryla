using System.Text.Json.Serialization;

namespace Ryla.Core.Domain.Webhooks;

/// <summary>
/// TikTok Shop ORDER_STATUS_CHANGE event content
/// เป็น JSON string ที่ต้อง deserialize จาก TikTokWebhookPayload.Content
/// Doc: https://partner.tiktokshop.com/docv2/page/tts-webhooks-overview
/// </summary>
public sealed record TikTokOrderContent
{
    [JsonPropertyName("order_id")]
    public string OrderId { get; init; } = string.Empty;

    [JsonPropertyName("order_status")]
    public string OrderStatus { get; init; } = string.Empty;

    /// <summary>Shop ID ของ seller — ใช้ match กับ connections table</summary>
    [JsonPropertyName("shop_id")]
    public string ShopId { get; init; } = string.Empty;
}
