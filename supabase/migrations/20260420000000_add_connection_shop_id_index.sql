-- =============================================================
-- Connection reverse lookup index (RYLA-4)
-- =============================================================
-- Webhook payload → shop_id → tenant lookup
-- Functional index on (platform, credentials->>'shop_id') สำหรับ
-- tiktok_shop / shopee platforms
--
-- ทำไม partial index:
--   - เฉพาะ is_active = true → ไม่ลากข้อมูลเก่า/disabled มา
--   - credentials ? 'shop_id' → skip rows ที่ไม่มี shop_id (เช่น line_oa, google_sheets)

CREATE INDEX IF NOT EXISTS idx_connections_platform_shop_id
  ON public.connections (platform, (credentials->>'shop_id'))
  WHERE is_active = true
    AND credentials ? 'shop_id';
