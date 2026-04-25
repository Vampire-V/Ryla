# Changelog

All notable changes to Ryla are documented here.
Format follows [Keep a Changelog](https://keepachangelog.com/en/1.0.0/).

---

## v0.2.0 (2026-04-25)

### Features
- **api:** Shopee webhook receiver with HMAC-SHA256 verification (#6)
  รับ push notification จาก Shopee Open Platform สำหรับ order status change events
  พร้อม HMAC-SHA256 signature verification + replay protection
- **api:** LINE OA push notification adapter for TikTok + Shopee webhooks (#7)
  เชื่อม inbound webhook (TikTok Shop, Shopee) → outbound LINE Messaging push
  ผ่าน ProcessOrderWebhookUseCase + ConnectionRepository + LineMessagingClient
  (hexagonal architecture). ใช้ existing connections(credentials jsonb) table
  สำหรับ per-tenant credentials storage + partial functional index สำหรับ
  reverse shop_id lookup. Webhook คืน 200 เสมอเพื่อกัน platform retry storm
- **api:** Google Sheets append-row adapter — webhooks ส่งเข้า LINE + Sheets ขนาน (#8)
  เพิ่ม outbound adapter ที่ append แถว order ลง Google Sheets ของ tenant คู่ขนาน
  กับ LINE push (RYLA-4). ใช้ raw HttpClient + manual JWT signing (RS256 ผ่าน
  System.Security.Cryptography.RSA) — ไม่ใช้ Google.Apis.* SDK เพราะ AOT-incompatible
  (ดู ADR-007). Token cache in-memory (60s expiry buffer) ลด token request เกือบทั้งหมด
  Fail-soft: Sheets/Token error → log + webhook ยังคืน 200 (idempotent)

### Migrations
- ⚠️ LINE OA push notification adapter (#7) — requires `supabase db push`

---

## v0.1.0 (2026-04-19)

- Health Check endpoint (`GET /health`) with DB connectivity probe
- Tenant Onboarding (tenant registration + user profiles)
- TikTok Shop Webhook Receiver with HMAC-SHA256 verification
