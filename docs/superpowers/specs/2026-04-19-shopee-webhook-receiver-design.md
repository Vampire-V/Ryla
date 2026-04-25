# Shopee Webhook Receiver — Design Spec

**Date:** 2026-04-19
**Feature:** RYLA-3 — Shopee Webhook Receiver
**Status:** Draft
**Author:** Claude (brainstorming session)

---

## Context

Ryla connects Thai e-commerce platforms with LINE OA. TikTok Shop webhook
receiver (RYLA-2) is complete. Shopee webhook receiver is the next feature
in the core integration flow: Shopee order events → (future) LINE notification.

This feature follows the same pattern as TikTok: **receive, validate, log**.
No downstream processing yet — that will come with the LINE OA adapter.

## Scope

- **In scope:** HMAC-SHA256 signature verification, replay protection,
  order status change event (code 3), structured logging, E2E tests
- **Out of scope:** Downstream processing (LINE push), event persistence (DB),
  reconciliation polling, other event types (logistics, chat, etc.)

---

## API Contract

```
POST /webhooks/shopee?authorization={hmac_hex_signature}
Content-Type: application/json
```

### Request Body

```json
{
  "code": 3,
  "shop_id": 123456,
  "timestamp": 1615452000,
  "data": {
    "ordersn": "210311ABCDEFGH",
    "status": "READY_TO_SHIP",
    "update_time": 1615452000
  }
}
```

### Key Fields

| Field | Type | Description |
|-------|------|-------------|
| `code` | int | Event type (3 = order status change) |
| `shop_id` | long | Shop that triggered the event |
| `timestamp` | long | Unix epoch seconds when event occurred |
| `data` | object | Event-specific payload |
| `data.ordersn` | string | Order serial number |
| `data.status` | string | New order status |
| `data.update_time` | long | When status changed |

### Responses

| Status | Condition |
|--------|-----------|
| 200 OK | Signature valid, payload parsed |
| 401 Unauthorized | Missing/invalid signature or stale timestamp |
| 422 Unprocessable Entity | Malformed JSON |

---

## Signature Verification

### Shopee's Signing Scheme

- **Algorithm:** HMAC-SHA256
- **Key:** Partner Key (app secret from Shopee Partner Console)
- **Base string:** `{callback_url}{raw_request_body}` (concatenation, no separator)
- **Signature location:** Query parameter `?authorization={hex_signature}`

### Differences from TikTok

| Aspect | TikTok | Shopee |
|--------|--------|--------|
| Signature location | HTTP header `TikTok-Signature` | Query param `?authorization=` |
| Header format | `t={ts},s={hex}` (timestamp embedded) | Hex string only |
| Base string | `{timestamp}.{body}` | `{callback_url}{body}` |
| Replay protection | Timestamp in signature header | Timestamp in JSON body |
| Key name | Client Secret | Partner Key |

### Replay Protection

- Extract `timestamp` from parsed JSON body
- Reject if `|now - timestamp| > MaxAgeSeconds` (default: 300s / 5 min)
- Note: Shopee embeds timestamp in body, not in signature header like TikTok

---

## Architecture

### Approach: Mirror TikTok Pattern

Each platform gets its own independent verifier — no shared abstraction.
Rationale: signing formats differ significantly (header vs query param,
different base strings). Premature abstraction would leak platform quirks.

### New Files

```
backend/src/Ryla.Core/
├── Configuration/ShopeeOptions.cs        # Partner key, callback URL, max age
├── Services/IShopeeHmacVerifier.cs       # Verification interface
├── Services/ShopeeHmacVerifier.cs        # HMAC-SHA256 implementation
└── Domain/Webhooks/ShopeeWebhookPayload.cs  # Incoming DTO

backend/src/Ryla.Api/
└── Endpoints/ShopeeWebhookEndpoints.cs   # HTTP endpoint

backend/tests/Ryla.Core.Tests/
└── Services/ShopeeHmacVerifierTests.cs   # Unit tests

backend/tests/Ryla.Api.Tests/
└── Endpoints/ShopeeWebhookEndpointsTests.cs  # Integration tests

tests/e2e/
└── webhook_shopee.py                     # E2E smoke tests
```

### Modified Files

```
backend/src/Ryla.Api/Extensions/ServiceCollectionExtensions.cs
  → Add ShopeeOptions config binding
  → Register IShopeeHmacVerifier
  → Add [JsonSerializable(typeof(ShopeeWebhookPayload))] to RylaJsonContext

backend/src/Ryla.Api/Program.cs
  → Add app.MapShopeeWebhookEndpoints()

backend/src/Ryla.Api/appsettings.json
  → Add "Shopee" config section

backend/src/Ryla.Api/appsettings.Development.json
  → Add "Shopee" dev config
```

### Dependency Flow

```
Request → ShopeeWebhookEndpoints (Api)
            → IShopeeHmacVerifier (Core interface)
            → ShopeeHmacVerifier (Core implementation)
            → ShopeeOptions (Core config via IOptions<T>)
```

---

## Core Types

### ShopeeOptions

```csharp
public sealed class ShopeeOptions
{
    public const string SectionName = "Shopee";
    public string PartnerKey { get; set; } = string.Empty;
    public string CallbackUrl { get; set; } = string.Empty;
    public int MaxAgeSeconds { get; set; } = 300;
}
```

### IShopeeHmacVerifier

```csharp
public interface IShopeeHmacVerifier
{
    bool Verify(string rawBody, string authorizationSignature);
}
```

Note: `callbackUrl` comes from `ShopeeOptions` via DI, not passed per-call.

### ShopeeWebhookPayload

```csharp
public sealed record ShopeeWebhookPayload
{
    [JsonPropertyName("code")]
    public int Code { get; init; }

    [JsonPropertyName("shop_id")]
    public long ShopId { get; init; }

    [JsonPropertyName("timestamp")]
    public long Timestamp { get; init; }

    [JsonPropertyName("data")]
    public JsonElement Data { get; init; }
}
```

`Data` is `JsonElement` (not strongly typed) because different event codes
have different data shapes. For code 3 (order status), we log `ordersn` and
`status` by reading from JsonElement — no need for a separate DTO in this scope.

### ShopeeHmacVerifier

```csharp
public sealed class ShopeeHmacVerifier : IShopeeHmacVerifier
{
    private readonly ShopeeOptions _options;

    public ShopeeHmacVerifier(IOptions<ShopeeOptions> options)
        => _options = options.Value;

    public bool Verify(string rawBody, string authorizationSignature)
        => VerifyCore(rawBody, authorizationSignature,
            _options.PartnerKey, _options.CallbackUrl);

    internal static bool VerifyCore(
        string rawBody,
        string authorizationSignature,
        string partnerKey,
        string callbackUrl)
    {
        if (string.IsNullOrEmpty(authorizationSignature)) return false;
        if (string.IsNullOrEmpty(rawBody)) return false;

        // Base string: callbackUrl + rawBody (no separator)
        var baseString = callbackUrl + rawBody;
        var keyBytes = Encoding.UTF8.GetBytes(partnerKey);
        var dataBytes = Encoding.UTF8.GetBytes(baseString);

        using var hmac = new HMACSHA256(keyBytes);
        var computed = hmac.ComputeHash(dataBytes);
        var computedHex = Convert.ToHexString(computed).ToLowerInvariant();

        return CryptographicOperations.FixedTimeEquals(
            Encoding.ASCII.GetBytes(computedHex),
            Encoding.ASCII.GetBytes(authorizationSignature));
    }
}
```

---

## Endpoint Flow

```
1. EnableBuffering()
2. Read raw body (StreamReader)
3. Reset body position
4. Extract `authorization` from query string
5. If missing → 401 Unauthorized
6. verifier.Verify(rawBody, authorization)
7. If false → 401 Unauthorized
8. Deserialize with RylaJsonContext.Default.ShopeeWebhookPayload
9. If JsonException → 422 Unprocessable Entity
10. If null → 422 Unprocessable Entity
11. Replay check: |now - payload.Timestamp| > MaxAgeSeconds → 401
12. Log: event code, shop_id, ordersn (if code 3), timestamp
13. Return 200 OK
```

Note: Replay check happens AFTER deserialization because Shopee's timestamp
is in the JSON body (not in the signature header like TikTok).

---

## Configuration

```json
// appsettings.json
{
  "Shopee": {
    "PartnerKey": "dev-secret-replace-me",
    "CallbackUrl": "https://your-domain.com/webhooks/shopee"
  }
}

// appsettings.Development.json
{
  "Shopee": {
    "PartnerKey": "test-partner-key-for-dev",
    "CallbackUrl": "http://localhost:5282/webhooks/shopee"
  }
}
```

---

## Testing Plan

### Unit Tests (ShopeeHmacVerifierTests)

| Test Case | Input | Expected |
|-----------|-------|----------|
| Valid signature | Correct HMAC | `true` |
| Invalid signature | Wrong hex | `false` |
| Empty signature | `""` | `false` |
| Null signature | `null` | `false` |
| Empty body | `""` | `false` |
| Different callback URL | Mismatched URL | `false` |

### API Integration Tests (ShopeeWebhookEndpointsTests)

| Test Case | Scenario | Expected |
|-----------|----------|----------|
| Valid signature + valid JSON | Happy path | 200 |
| Invalid signature | Wrong HMAC | 401 |
| Missing authorization param | No query param | 401 |
| Malformed JSON body | `{invalid` | 422 |
| Empty body | No content | 401 |
| Valid sig + non-order event | code != 3 | 200 (still accepted) |
| Valid sig + stale timestamp | > 5 min old | 401 |

### E2E Smoke Tests (webhook_shopee.py)

| # | Test Case | Expected |
|---|-----------|----------|
| 1 | Valid order status webhook | 200 |
| 2 | Invalid signature | 401 |
| 3 | Missing authorization param | 401 |
| 4 | Expired timestamp (> 5 min) | 401 |
| 5 | Future timestamp (> 5 min) | 401 |
| 6 | Malformed JSON | 422 |
| 7 | Empty body | 401 |
| 8 | Different event code (code 0) | 200 |
| 9 | Tampered body (valid sig, modified body) | 401 |

---

## AOT Compliance Checklist

- [ ] No reflection in ShopeeHmacVerifier
- [ ] ShopeeWebhookPayload registered in RylaJsonContext via [JsonSerializable]
- [ ] No `dynamic` keyword
- [ ] Explicit DI registration (no assembly scanning)
- [ ] `dotnet publish -r linux-x64 --self-contained` produces no trim warnings
