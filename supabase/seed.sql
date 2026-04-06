-- ─────────────────────────────────────────────────────────────────────────────
-- Ryla Development Seed Data
-- ใช้สำหรับ local development เท่านั้น — ไม่รันใน production
-- รัน: supabase db reset  หรือ  make db-reset
-- ─────────────────────────────────────────────────────────────────────────────

-- ── Tenants ───────────────────────────────────────────────────────────────────
-- สองบริษัทตัวอย่าง: ร้านค้าขนาดเล็กและขนาดกลาง

INSERT INTO tenants (id, name) VALUES
  ('00000000-0000-0000-0000-000000000001', 'ร้านไทยทอย (Demo SME เล็ก)'),
  ('00000000-0000-0000-0000-000000000002', 'บริษัท ไทยมาร์ท จำกัด (Demo SME กลาง)')
ON CONFLICT (id) DO NOTHING;

-- ── Connections ───────────────────────────────────────────────────────────────
-- จำลอง integration ที่ tenant ตั้งค่าไว้

INSERT INTO connections (tenant_id, platform, credentials, is_active) VALUES
  -- ร้านไทยทอย: เชื่อม TikTok Shop + LINE OA
  (
    '00000000-0000-0000-0000-000000000001',
    'tiktok_shop',
    '{"shop_id": "demo-shop-001", "note": "credentials encrypted in production"}',
    true
  ),
  (
    '00000000-0000-0000-0000-000000000001',
    'line_oa',
    '{"channel_id": "demo-line-001", "note": "credentials encrypted in production"}',
    true
  ),
  -- ไทยมาร์ท: เชื่อม Shopee + LINE OA + Google Sheets
  (
    '00000000-0000-0000-0000-000000000002',
    'shopee',
    '{"shop_id": "demo-shopee-002", "note": "credentials encrypted in production"}',
    true
  ),
  (
    '00000000-0000-0000-0000-000000000002',
    'line_oa',
    '{"channel_id": "demo-line-002", "note": "credentials encrypted in production"}',
    true
  ),
  (
    '00000000-0000-0000-0000-000000000002',
    'google_sheets',
    '{"spreadsheet_id": "demo-sheet-002", "note": "credentials encrypted in production"}',
    true
  );
