using System.Text.Json.Serialization;
using Ryla.Api.Endpoints;

namespace Ryla.Api.Extensions;

// Source-generated JSON context — วิธีเดียวที่ AOT-safe สำหรับ serialization
// ทุก type ที่ผ่าน HTTP boundary ต้องลงทะเบียนที่นี่
[JsonSerializable(typeof(HealthResponse))]
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    WriteIndented = false)]
internal partial class RylaJsonContext : JsonSerializerContext { }

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddRylaCoreServices(
        this IServiceCollection services)
    {
        // TODO: ลงทะเบียน Core use cases ที่นี่ (explicit, ห้ามใช้ assembly scanning)
        return services;
    }
}
