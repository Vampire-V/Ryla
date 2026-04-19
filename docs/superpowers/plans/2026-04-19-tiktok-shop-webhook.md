# RYLA-2: TikTok Shop Webhook Receiver Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** รับ POST webhook จาก TikTok Shop, verify HMAC signature, parse order event payload และ return 200/401 ตาม validity

**Architecture:** Pure HMAC verification logic อยู่ใน `Ryla.Core` (ไม่มี I/O), configuration ผ่าน `TikTokShopOptions`, endpoint อ่าน raw body ก่อน verify แล้ว parse payload. Processing จริง (LINE notification, DB) อยู่นอก scope ของ RYLA-2

**Tech Stack:** .NET 10 Native AOT, HMACSHA256 (BCL), xUnit + NSubstitute, WebApplicationFactory

---

## File Map

### สร้างใหม่
```
backend/src/Ryla.Core/
  Configuration/
    TikTokShopOptions.cs          ← config record (ClientSecret, SignatureHeader)
  Domain/Webhooks/
    TikTokWebhookPayload.cs       ← parsed payload record
  Services/
    ITikTokHmacVerifier.cs        ← inbound port interface
    TikTokHmacVerifier.cs         ← pure HMAC implementation (BCL only)

backend/src/Ryla.Api/
  Endpoints/
    TikTokWebhookEndpoints.cs     ← POST /webhooks/tiktok-shop

backend/tests/Ryla.Core.Tests/
  Services/
    TikTokHmacVerifierTests.cs    ← unit tests for pure HMAC logic

backend/tests/Ryla.Api.Tests/
  Endpoints/
    TikTokWebhookEndpointsTests.cs ← API-level tests (200/401)
```

### แก้ไข
```
backend/src/Ryla.Api/Extensions/ServiceCollectionExtensions.cs
  → เพิ่ม [JsonSerializable(typeof(TikTokWebhookPayload))]
  → เพิ่ม services.Configure<TikTokShopOptions>(...)

backend/src/Ryla.Infrastructure/DependencyInjection.cs
  → เพิ่ม services.AddScoped<ITikTokHmacVerifier, TikTokHmacVerifier>()

backend/src/Ryla.Api/Program.cs
  → เพิ่ม app.MapTikTokWebhookEndpoints()
```

---

## Task 1: สร้าง Feature Branch

- [ ] **Step 1: สร้าง branch**

```bash
cd /Users/pipat/Documents/MiniProject/Ryla
git checkout main
git pull
git checkout -b feat/RYLA-2-tiktok-webhook
```

Expected: อยู่ใน branch `feat/RYLA-2-tiktok-webhook`

---

## Task 2: Configuration — TikTokShopOptions

**Files:**
- Create: `backend/src/Ryla.Core/Configuration/TikTokShopOptions.cs`

- [ ] **Step 1: สร้าง options record**

สร้างไฟล์ `backend/src/Ryla.Core/Configuration/TikTokShopOptions.cs`:

```csharp
namespace Ryla.Core.Configuration;

/// <summary>
/// TikTok Shop Partner API credentials และ webhook configuration
/// ตั้งค่าผ่าน appsettings.json หรือ dotnet user-secrets
/// </summary>
public sealed record TikTokShopOptions
{
    public const string SectionName = "TikTokShop";

    /// <summary>Client Secret จาก TikTok Shop Partner Center</summary>
    public required string ClientSecret { get; init; }

    /// <summary>
    /// ชื่อ HTTP header ที่ TikTok Shop ส่ง HMAC signature มาด้วย
    /// Default: "TikTok-Signature" (format: t={timestamp},s={hex_signature})
    /// </summary>
    public string SignatureHeader { get; init; } = "TikTok-Signature";

    /// <summary>อายุสูงสุดของ webhook request (วินาที) เพื่อป้องกัน replay attack</summary>
    public int MaxAgeSeconds { get; init; } = 300; // 5 minutes
}
```

- [ ] **Step 2: commit**

```bash
git add backend/src/Ryla.Core/Configuration/TikTokShopOptions.cs
git commit -m "feat(core): add TikTokShopOptions configuration record"
```

---

## Task 3: Domain Model — TikTokWebhookPayload

**Files:**
- Create: `backend/src/Ryla.Core/Domain/Webhooks/TikTokWebhookPayload.cs`

- [ ] **Step 1: สร้าง payload records**

สร้างไฟล์ `backend/src/Ryla.Core/Domain/Webhooks/TikTokWebhookPayload.cs`:

```csharp
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
```

- [ ] **Step 2: commit**

```bash
git add backend/src/Ryla.Core/Domain/Webhooks/TikTokWebhookPayload.cs
git commit -m "feat(core): add TikTokWebhookPayload domain record"
```

---

## Task 4: HMAC Verifier — Interface + Implementation (TDD)

**Files:**
- Create: `backend/src/Ryla.Core/Services/ITikTokHmacVerifier.cs`
- Create: `backend/src/Ryla.Core/Services/TikTokHmacVerifier.cs`
- Test: `backend/tests/Ryla.Core.Tests/Services/TikTokHmacVerifierTests.cs`

- [ ] **Step 1: สร้าง interface**

สร้างไฟล์ `backend/src/Ryla.Core/Services/ITikTokHmacVerifier.cs`:

```csharp
namespace Ryla.Core.Services;

/// <summary>
/// ตรวจสอบ HMAC signature ของ TikTok Shop webhook
/// </summary>
public interface ITikTokHmacVerifier
{
    /// <summary>
    /// Returns true ถ้า signature valid และ request ไม่เกิน MaxAgeSeconds
    /// </summary>
    bool Verify(string rawBody, string signatureHeader);
}
```

- [ ] **Step 2: เขียน failing tests ก่อน**

สร้างไฟล์ `backend/tests/Ryla.Core.Tests/Services/TikTokHmacVerifierTests.cs`:

```csharp
using System.Security.Cryptography;
using System.Text;
using Ryla.Core.Services;
using Xunit;

namespace Ryla.Core.Tests.Services;

public class TikTokHmacVerifierTests
{
    private const string ClientSecret = "test-secret-key";

    // Helper: สร้าง valid signature header สำหรับ test
    private static string BuildSignatureHeader(string rawBody, long timestamp)
    {
        var payload = $"{timestamp}.{rawBody}";
        var keyBytes = Encoding.UTF8.GetBytes(ClientSecret);
        var payloadBytes = Encoding.UTF8.GetBytes(payload);
        using var hmac = new HMACSHA256(keyBytes);
        var hash = hmac.ComputeHash(payloadBytes);
        var hex = Convert.ToHexString(hash).ToLowerInvariant();
        return $"t={timestamp},s={hex}";
    }

    [Fact]
    public void VerifyCore__WhenSignatureIsValid__ShouldReturnTrue()
    {
        // Arrange
        var rawBody = """{"event":"ORDER_STATUS_CHANGE","create_time":1000}""";
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var header = BuildSignatureHeader(rawBody, now);

        // Act
        var result = TikTokHmacVerifier.VerifyCore(rawBody, header, ClientSecret, now);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void VerifyCore__WhenSignatureIsWrong__ShouldReturnFalse()
    {
        // Arrange
        var rawBody = """{"event":"ORDER_STATUS_CHANGE"}""";
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var header = $"t={now},s=deadbeefdeadbeefdeadbeefdeadbeefdeadbeefdeadbeefdeadbeefdeadbeef";

        // Act
        var result = TikTokHmacVerifier.VerifyCore(rawBody, header, ClientSecret, now);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void VerifyCore__WhenRequestIsTooOld__ShouldReturnFalse()
    {
        // Arrange
        var rawBody = """{"event":"ORDER_STATUS_CHANGE"}""";
        var oldTimestamp = DateTimeOffset.UtcNow.AddMinutes(-10).ToUnixTimeSeconds();
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var header = BuildSignatureHeader(rawBody, oldTimestamp);

        // Act — now is 10 min after timestamp, maxAge = 300s (5 min)
        var result = TikTokHmacVerifier.VerifyCore(rawBody, header, ClientSecret, now, maxAgeSeconds: 300);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void VerifyCore__WhenHeaderIsMalformed__ShouldReturnFalse()
    {
        // Arrange
        var rawBody = """{"event":"ORDER_STATUS_CHANGE"}""";
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        // Act — ส่ง header ที่ format ผิด
        var result = TikTokHmacVerifier.VerifyCore(rawBody, "invalid-header", ClientSecret, now);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void VerifyCore__WhenSignatureHeaderIsEmpty__ShouldReturnFalse()
    {
        // Arrange
        var rawBody = """{"event":"ORDER_STATUS_CHANGE"}""";
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        // Act
        var result = TikTokHmacVerifier.VerifyCore(rawBody, string.Empty, ClientSecret, now);

        // Assert
        Assert.False(result);
    }
}
```

- [ ] **Step 3: run tests เพื่อยืนยัน FAIL**

```bash
cd /Users/pipat/Documents/MiniProject/Ryla
dotnet test backend/tests/Ryla.Core.Tests/ --filter "TikTokHmacVerifierTests"
```

Expected: ERROR — `TikTokHmacVerifier` ยังไม่มี

- [ ] **Step 4: implement TikTokHmacVerifier**

สร้างไฟล์ `backend/src/Ryla.Core/Services/TikTokHmacVerifier.cs`:

```csharp
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Ryla.Core.Configuration;

namespace Ryla.Core.Services;

/// <summary>
/// ตรวจสอบ HMAC-SHA256 signature ของ TikTok Shop webhook
/// Algorithm: HMAC-SHA256(key=clientSecret, data="{timestamp}.{rawBody}")
/// Header format: "t={unixTimestamp},s={hexSignature}"
/// </summary>
public sealed class TikTokHmacVerifier : ITikTokHmacVerifier
{
    private readonly TikTokShopOptions _options;

    public TikTokHmacVerifier(IOptions<TikTokShopOptions> options)
        => _options = options.Value;

    /// <inheritdoc />
    public bool Verify(string rawBody, string signatureHeader)
        => VerifyCore(rawBody, signatureHeader, _options.ClientSecret,
            DateTimeOffset.UtcNow.ToUnixTimeSeconds(), _options.MaxAgeSeconds);

    /// <summary>
    /// Pure static method สำหรับ unit testing (ไม่ต้องมี DI)
    /// </summary>
    internal static bool VerifyCore(
        string rawBody,
        string signatureHeader,
        string clientSecret,
        long nowUnixSeconds,
        int maxAgeSeconds = 300)
    {
        if (string.IsNullOrEmpty(signatureHeader)) return false;

        // Parse "t={timestamp},s={hex_signature}"
        string? timestamp = null;
        string? signature = null;

        foreach (var part in signatureHeader.Split(',', StringSplitOptions.RemoveEmptyEntries))
        {
            if (part.StartsWith("t=", StringComparison.Ordinal))
                timestamp = part[2..];
            else if (part.StartsWith("s=", StringComparison.Ordinal))
                signature = part[2..];
        }

        if (timestamp is null || signature is null) return false;
        if (!long.TryParse(timestamp, out var ts)) return false;

        // Replay attack protection: reject requests older than maxAgeSeconds
        if (Math.Abs(nowUnixSeconds - ts) > maxAgeSeconds) return false;

        // Compute HMAC-SHA256: key=clientSecret, data="{timestamp}.{rawBody}"
        var payload = $"{timestamp}.{rawBody}";
        var keyBytes = Encoding.UTF8.GetBytes(clientSecret);
        var payloadBytes = Encoding.UTF8.GetBytes(payload);

        using var hmac = new HMACSHA256(keyBytes);
        var computed = hmac.ComputeHash(payloadBytes);
        var computedHex = Convert.ToHexString(computed).ToLowerInvariant();

        // ใช้ constant-time comparison เพื่อป้องกัน timing attack
        return CryptographicOperations.FixedTimeEquals(
            Encoding.ASCII.GetBytes(computedHex),
            Encoding.ASCII.GetBytes(signature));
    }
}
```

- [ ] **Step 5: run tests เพื่อยืนยัน PASS**

```bash
dotnet test backend/tests/Ryla.Core.Tests/ --filter "TikTokHmacVerifierTests"
```

Expected: 5 tests PASSED

- [ ] **Step 6: commit**

```bash
git add backend/src/Ryla.Core/Services/ITikTokHmacVerifier.cs \
        backend/src/Ryla.Core/Services/TikTokHmacVerifier.cs \
        backend/tests/Ryla.Core.Tests/Services/TikTokHmacVerifierTests.cs
git commit -m "feat(core): add TikTokHmacVerifier with HMAC-SHA256 verification

- Pure static VerifyCore สำหรับ unit tests ที่ไม่ต้องมี DI
- Replay attack protection ด้วย timestamp age check (300s default)
- Constant-time comparison ป้องกัน timing attack
- 5 unit tests ครอบคลุม: valid, wrong signature, expired, malformed, empty

RYLA-2"
```

---

## Task 5: API Endpoint — POST /webhooks/tiktok-shop

**Files:**
- Create: `backend/src/Ryla.Api/Endpoints/TikTokWebhookEndpoints.cs`
- Modify: `backend/src/Ryla.Api/Extensions/ServiceCollectionExtensions.cs`
- Modify: `backend/src/Ryla.Api/Program.cs`
- Modify: `backend/src/Ryla.Infrastructure/DependencyInjection.cs`

- [ ] **Step 1: สร้าง endpoint**

สร้างไฟล์ `backend/src/Ryla.Api/Endpoints/TikTokWebhookEndpoints.cs`:

```csharp
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using Ryla.Core.Configuration;
using Ryla.Core.Services;

namespace Ryla.Api.Endpoints;

internal static class TikTokWebhookEndpoints
{
    public static IEndpointRouteBuilder MapTikTokWebhookEndpoints(
        this IEndpointRouteBuilder app)
    {
        app.MapPost("/webhooks/tiktok-shop", ReceiveWebhook)
            .AllowAnonymous() // webhook ไม่มี user auth — ใช้ HMAC signature แทน
            .WithName("ReceiveTikTokShopWebhook")
            .WithSummary("TikTok Shop webhook receiver")
            .WithDescription("รับ event จาก TikTok Shop Partner API พร้อม HMAC verification")
            .WithTags("Webhooks")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status422UnprocessableEntity);

        return app;
    }

    internal static async Task<IResult> ReceiveWebhook(
        HttpContext ctx,
        ITikTokHmacVerifier verifier,
        ILogger<Program> logger)
    {
        // อ่าน raw body เพื่อ HMAC verification
        // ต้องอ่านก่อน model binding เพราะ HMAC คำนวณจาก raw bytes
        ctx.Request.EnableBuffering();
        using var reader = new StreamReader(ctx.Request.Body, Encoding.UTF8, leaveOpen: true);
        var rawBody = await reader.ReadToEndAsync();
        ctx.Request.Body.Position = 0;

        // ตรวจสอบ signature
        var signatureHeader = ctx.Request.Headers[TikTokShopOptions.SignatureHeaderName].FirstOrDefault();
        if (string.IsNullOrEmpty(signatureHeader) || !verifier.Verify(rawBody, signatureHeader))
        {
            logger.LogWarning("TikTok Shop webhook: invalid or missing signature from {RemoteIp}",
                ctx.Connection.RemoteIpAddress);
            return Results.Unauthorized();
        }

        // Parse payload
        var payload = JsonSerializer.Deserialize(rawBody, RylaJsonContext.Default.TikTokWebhookPayload);
        if (payload is null)
        {
            logger.LogWarning("TikTok Shop webhook: failed to parse payload");
            return Results.UnprocessableEntity();
        }

        logger.LogInformation(
            "TikTok Shop webhook received: event={Event} clientKey={ClientKey} createTime={CreateTime}",
            payload.Event, payload.ClientKey, payload.CreateTime);

        return Results.Ok();
    }
}
```

- [ ] **Step 2: เพิ่ม constant ใน TikTokShopOptions**

แก้ไข `backend/src/Ryla.Core/Configuration/TikTokShopOptions.cs` — เพิ่ม constant สำหรับ header name:

```csharp
namespace Ryla.Core.Configuration;

public sealed record TikTokShopOptions
{
    public const string SectionName = "TikTokShop";

    /// <summary>ชื่อ header ที่ TikTok Shop ใส่ HMAC signature</summary>
    public const string SignatureHeaderName = "TikTok-Signature";

    public required string ClientSecret { get; init; }
    public string SignatureHeader { get; init; } = SignatureHeaderName;
    public int MaxAgeSeconds { get; init; } = 300;
}
```

- [ ] **Step 3: ลงทะเบียน JSON type ใน RylaJsonContext**

แก้ไข `backend/src/Ryla.Api/Extensions/ServiceCollectionExtensions.cs`:

```csharp
using System.Text.Json.Serialization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Ryla.Core.Configuration;
using Ryla.Core.Domain.Webhooks;

namespace Ryla.Api.Extensions;

// Source-generated JSON context — วิธีเดียวที่ AOT-safe สำหรับ serialization
// ทุก type ที่ผ่าน HTTP boundary ต้องลงทะเบียนที่นี่
[JsonSerializable(typeof(HealthResponse))]
[JsonSerializable(typeof(TikTokWebhookPayload))]   // ← เพิ่ม
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    WriteIndented = false)]
internal partial class RylaJsonContext : JsonSerializerContext { }

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddRylaCoreServices(
        this IServiceCollection services,
        IConfiguration configuration)   // ← เพิ่ม parameter
    {
        services.Configure<TikTokShopOptions>(        // ← เพิ่ม
            configuration.GetSection(TikTokShopOptions.SectionName));

        return services;
    }
}
```

> **หมายเหตุ:** `AddRylaCoreServices` ต้องรับ `IConfiguration` เพิ่มด้วย — ต้องแก้ call site ใน Program.cs ด้วย

- [ ] **Step 4: ลงทะเบียน TikTokHmacVerifier ใน Infrastructure DI**

แก้ไข `backend/src/Ryla.Infrastructure/DependencyInjection.cs` — เพิ่มหลัง Singleton DB:

```csharp
using Ryla.Core.Services;
using Ryla.Infrastructure.Adapters.Webhooks; // ← จะสร้างใน task นี้

// ─── Webhook Services ────────────────────────────────────────────────────────
services.AddScoped<ITikTokHmacVerifier, TikTokHmacVerifier>();
```

> **หมายเหตุ:** `TikTokHmacVerifier` อยู่ใน `Ryla.Core` ไม่ใช่ Infrastructure เพราะเป็น pure logic
> ให้ลงทะเบียนใน `Ryla.Api/Extensions/ServiceCollectionExtensions.cs` ใน `AddRylaCoreServices` แทน:

```csharp
services.AddScoped<ITikTokHmacVerifier, TikTokHmacVerifier>();
```

- [ ] **Step 5: แก้ Program.cs**

แก้ไข `backend/src/Ryla.Api/Program.cs`:

```csharp
var builder = WebApplication.CreateSlimBuilder(args);

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.TypeInfoResolverChain.Insert(0, RylaJsonContext.Default);
});

builder.Services.AddOpenApi();
builder.Services.AddRylaCoreServices(builder.Configuration);   // ← ส่ง Configuration
builder.Services.AddRylaInfrastructureServices(builder.Configuration);

var app = builder.Build();

app.MapOpenApi();
app.MapHealthEndpoints();
app.MapTikTokWebhookEndpoints();   // ← เพิ่ม

app.Run();
```

- [ ] **Step 6: เพิ่ม dev config สำหรับ TikTok**

เพิ่มใน `backend/src/Ryla.Api/appsettings.Development.json`:

```json
{
  "TikTokShop": {
    "ClientSecret": "dev-secret-replace-me"
  }
}
```

- [ ] **Step 7: build เพื่อตรวจสอบ compile errors**

```bash
dotnet build backend/Ryla.slnx
```

Expected: Build succeeded, 0 errors

- [ ] **Step 8: commit**

```bash
git add backend/src/Ryla.Api/Endpoints/TikTokWebhookEndpoints.cs \
        backend/src/Ryla.Api/Extensions/ServiceCollectionExtensions.cs \
        backend/src/Ryla.Core/Configuration/TikTokShopOptions.cs \
        backend/src/Ryla.Api/Program.cs \
        backend/src/Ryla.Api/appsettings.Development.json
git commit -m "feat(api): add POST /webhooks/tiktok-shop endpoint with HMAC verification

- อ่าน raw body ก่อน HMAC verify เพื่อให้ signature ถูกต้อง
- Return 401 ถ้า signature invalid หรือ expired
- Return 422 ถ้า payload parse ไม่ได้
- Return 200 พร้อม log event type และ client key

RYLA-2"
```

---

## Task 6: API Endpoint Tests

**Files:**
- Test: `backend/tests/Ryla.Api.Tests/Endpoints/TikTokWebhookEndpointsTests.cs`

- [ ] **Step 1: เขียน failing tests**

สร้างไฟล์ `backend/tests/Ryla.Api.Tests/Endpoints/TikTokWebhookEndpointsTests.cs`:

```csharp
using System.Net;
using System.Text;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Ryla.Core.Configuration;
using Ryla.Core.Services;
using Xunit;

namespace Ryla.Api.Tests.Endpoints;

public class TikTokWebhookEndpointsTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public TikTokWebhookEndpointsTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    private HttpClient CreateClientWithVerifier(bool verifierReturns)
    {
        var verifier = Substitute.For<ITikTokHmacVerifier>();
        verifier.Verify(Arg.Any<string>(), Arg.Any<string>()).Returns(verifierReturns);

        return _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                // แทนที่ real verifier ด้วย mock
                var descriptor = services.SingleOrDefault(
                    d => d.ServiceType == typeof(ITikTokHmacVerifier));
                if (descriptor is not null) services.Remove(descriptor);
                services.AddScoped<ITikTokHmacVerifier>(_ => verifier);
            });
        }).CreateClient();
    }

    [Fact]
    public async Task ReceiveTikTokWebhook__WhenSignatureIsValid__ShouldReturn200()
    {
        // Arrange
        var client = CreateClientWithVerifier(verifierReturns: true);
        var payload = """{"client_key":"test-key","event":"ORDER_STATUS_CHANGE","create_time":1234567890,"content":"{}"}""";

        var request = new HttpRequestMessage(HttpMethod.Post, "/webhooks/tiktok-shop");
        request.Content = new StringContent(payload, Encoding.UTF8, "application/json");
        request.Headers.Add(TikTokShopOptions.SignatureHeaderName, "t=1234567890,s=fakesig");

        // Act
        var response = await client.SendAsync(request);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task ReceiveTikTokWebhook__WhenSignatureIsInvalid__ShouldReturn401()
    {
        // Arrange
        var client = CreateClientWithVerifier(verifierReturns: false);
        var payload = """{"client_key":"test-key","event":"ORDER_STATUS_CHANGE","create_time":1234567890,"content":"{}"}""";

        var request = new HttpRequestMessage(HttpMethod.Post, "/webhooks/tiktok-shop");
        request.Content = new StringContent(payload, Encoding.UTF8, "application/json");
        request.Headers.Add(TikTokShopOptions.SignatureHeaderName, "t=1234567890,s=wrong");

        // Act
        var response = await client.SendAsync(request);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ReceiveTikTokWebhook__WhenSignatureHeaderIsMissing__ShouldReturn401()
    {
        // Arrange
        var client = CreateClientWithVerifier(verifierReturns: false);
        var payload = """{"client_key":"test-key","event":"ORDER_STATUS_CHANGE","create_time":1234567890,"content":"{}"}""";

        var request = new HttpRequestMessage(HttpMethod.Post, "/webhooks/tiktok-shop");
        request.Content = new StringContent(payload, Encoding.UTF8, "application/json");
        // ไม่ใส่ signature header

        // Act
        var response = await client.SendAsync(request);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ReceiveTikTokWebhook__WhenPayloadIsInvalidJson__ShouldReturn422()
    {
        // Arrange
        var client = CreateClientWithVerifier(verifierReturns: true);

        var request = new HttpRequestMessage(HttpMethod.Post, "/webhooks/tiktok-shop");
        request.Content = new StringContent("not-json", Encoding.UTF8, "text/plain");
        request.Headers.Add(TikTokShopOptions.SignatureHeaderName, "t=1234567890,s=fakesig");

        // Act
        var response = await client.SendAsync(request);

        // Assert
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }
}
```

- [ ] **Step 2: run tests เพื่อยืนยัน state**

```bash
dotnet test backend/tests/Ryla.Api.Tests/ --filter "TikTokWebhookEndpointsTests"
```

Expected: 4 tests PASS (ถ้า Task 5 สมบูรณ์)

- [ ] **Step 3: run all tests เพื่อตรวจสอบ regression**

```bash
dotnet test backend/Ryla.slnx
```

Expected: ทุก test PASS

- [ ] **Step 4: commit**

```bash
git add backend/tests/Ryla.Api.Tests/Endpoints/TikTokWebhookEndpointsTests.cs
git commit -m "test(api): add TikTok Shop webhook endpoint tests

- 4 test cases: valid sig 200, invalid sig 401, missing header 401, bad JSON 422
- ใช้ NSubstitute mock ITikTokHmacVerifier ผ่าน WebApplicationFactory

RYLA-2"
```

---

## Task 7: AOT Validation + PR

- [ ] **Step 1: AOT publish เพื่อตรวจสอบ warnings**

```bash
cd backend/src/Ryla.Api
dotnet publish -r linux-x64 --self-contained 2>&1 | grep -E "(warning|error|IL2|IL3)"
```

Expected: ไม่มี IL2026 / IL2072 / IL3050 warnings

- [ ] **Step 2: สร้าง changelog fragment**

สร้างไฟล์ `changelog/fragments/2-tiktok-shop-webhook-receiver.yml`:

```yaml
pr: 2
type: feat
scope: "api"
title: "TikTok Shop webhook receiver with HMAC-SHA256 verification"
description: |
  รับ POST webhook จาก TikTok Shop พร้อม HMAC-SHA256 signature verification
  ป้องกัน replay attack ด้วย timestamp age check (5 นาที)
  Return 401 ถ้า signature ไม่ valid, 200 ถ้า valid
breaking: false
migration: false
```

- [ ] **Step 3: commit changelog**

```bash
git add changelog/fragments/2-tiktok-shop-webhook-receiver.yml
git commit -m "chore: add changelog fragment for RYLA-2"
```

- [ ] **Step 4: สร้าง PR**

```bash
gh pr create \
  --title "feat(api): RYLA-2 TikTok Shop webhook receiver with HMAC verification" \
  --base main \
  --body "## What
รับ POST webhook จาก TikTok Shop พร้อม HMAC-SHA256 signature verification

## Why
เป็น foundation สำหรับ order event processing — ต้อง verify ก่อนทุกครั้งเพื่อความปลอดภัย

## How
- Pure HMAC verifier ใน Core (ไม่มี I/O, unit testable)
- Constant-time comparison ป้องกัน timing attack
- Timestamp age check ป้องกัน replay attack (5 นาที)
- อ่าน raw body ก่อน HMAC verify เพื่อให้ signature ถูกต้อง

## Tests
- 5 unit tests สำหรับ HmacVerifier (valid, wrong sig, expired, malformed, empty header)
- 4 API tests สำหรับ endpoint (200, 401×2, 422)

## AOT Check
- ใช้ HMACSHA256 จาก BCL — AOT-safe
- JSON ผ่าน Source Generator — AOT-safe
- ไม่มี reflection ใน hot path

## Checklist
- [ ] Lint passes
- [ ] Tests pass (9 tests)
- [ ] Coverage >= 80%"
```

---

## Verification Summary

| Test | Command | Expected |
|------|---------|---------|
| Unit tests | `dotnet test backend/tests/Ryla.Core.Tests/ --filter "TikTokHmacVerifier"` | 5 PASS |
| API tests | `dotnet test backend/tests/Ryla.Api.Tests/ --filter "TikTokWebhookEndpoints"` | 4 PASS |
| All tests | `dotnet test backend/Ryla.slnx` | All PASS |
| AOT check | `dotnet publish -r linux-x64 --self-contained` | 0 IL warnings |

Manual test (หลัง run app):
```bash
# Valid request (signature จะ fail เพราะ dev-secret ไม่ตรง — expect 401)
curl -X POST http://localhost:5000/webhooks/tiktok-shop \
  -H "Content-Type: application/json" \
  -H "TikTok-Signature: t=9999999999,s=fakesig" \
  -d '{"client_key":"test","event":"ORDER_STATUS_CHANGE","create_time":1234567890,"content":"{}"}'
```
