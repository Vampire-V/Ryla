using System.Text.Json.Serialization;

namespace Ryla.Infrastructure.Adapters.LineMessaging;

/// <summary>
/// Source-generated JSON context สำหรับ LINE Messaging API
/// AOT-safe: ไม่มี reflection, ใช้ source generation เท่านั้น
/// </summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(LinePushRequest))]
[JsonSerializable(typeof(LineMessage))]
[JsonSerializable(typeof(LineErrorResponse))]
internal sealed partial class LineJsonContext : JsonSerializerContext;

internal sealed record LinePushRequest(string To, IReadOnlyList<LineMessage> Messages);

internal sealed record LineMessage(string Type, string Text);

internal sealed record LineErrorResponse(string? Message);
