-- supabase/migrations/20260425000000_connections_unique_constraint.sql
-- UNIQUE constraint บน (tenant_id, platform) เพื่อรองรับ upsert จาก frontend
-- แต่ละ tenant มีได้ 1 connection ต่อ platform
ALTER TABLE connections
  ADD CONSTRAINT connections_tenant_platform_unique
  UNIQUE (tenant_id, platform);
