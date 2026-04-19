namespace Ryla.Core.Domain.Webhooks;

/// <summary>
/// Context สำหรับ process order webhook — shared ระหว่าง TikTok Shop และ Shopee
/// </summary>
public sealed record OrderWebhookContext(
    Platform Platform,
    string ShopId,
    string OrderId,
    string EventType);
