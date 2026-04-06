-- =============================================================
-- Ryla: Initial Schema Migration
-- =============================================================
-- กฎ: RLS เปิดทุก table เสมอ
-- Application-layer filtering ไม่เพียงพอ — RLS คือ last line of defence

-- -------------------------------------------------------------
-- Tenants: หนึ่ง tenant = หนึ่งธุรกิจ SME ไทย
-- -------------------------------------------------------------
CREATE TABLE tenants (
  id         UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  name       TEXT NOT NULL,
  created_at TIMESTAMPTZ NOT NULL DEFAULT now()
);

ALTER TABLE tenants ENABLE ROW LEVEL SECURITY;

-- -------------------------------------------------------------
-- Profiles: user แต่ละคนอยู่ใน tenant เดียว
-- linked กับ auth.users ของ Supabase
-- -------------------------------------------------------------
CREATE TABLE profiles (
  id         UUID PRIMARY KEY REFERENCES auth.users(id) ON DELETE CASCADE,
  tenant_id  UUID NOT NULL REFERENCES tenants(id),
  full_name  TEXT,
  created_at TIMESTAMPTZ NOT NULL DEFAULT now()
);

ALTER TABLE profiles ENABLE ROW LEVEL SECURITY;

-- RLS: user เห็นได้แค่ profile ของตัวเอง
CREATE POLICY "profiles: self access"
  ON profiles FOR ALL
  USING (auth.uid() = id);

-- -------------------------------------------------------------
-- Connections: การเชื่อมต่อ platform ที่ tenant ตั้งค่าไว้
-- credentials เก็บ encrypted ที่ application layer
-- -------------------------------------------------------------
CREATE TABLE connections (
  id          UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  tenant_id   UUID NOT NULL REFERENCES tenants(id),
  platform    TEXT NOT NULL CHECK (platform IN ('tiktok_shop', 'shopee', 'line_oa', 'google_sheets')),
  credentials JSONB NOT NULL DEFAULT '{}',
  is_active   BOOLEAN NOT NULL DEFAULT true,
  created_at  TIMESTAMPTZ NOT NULL DEFAULT now()
);

ALTER TABLE connections ENABLE ROW LEVEL SECURITY;

-- RLS: tenant isolation — user เห็นได้แค่ connections ของ tenant ตัวเอง
CREATE POLICY "connections: tenant isolation"
  ON connections FOR ALL
  USING (
    tenant_id = (SELECT tenant_id FROM profiles WHERE id = auth.uid())
  );

CREATE INDEX idx_connections_tenant ON connections(tenant_id);
CREATE INDEX idx_connections_platform ON connections(tenant_id, platform);
