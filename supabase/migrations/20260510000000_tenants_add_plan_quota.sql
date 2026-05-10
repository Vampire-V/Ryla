-- Plan/quota tracking สำหรับ subscription management
-- ใช้ CHECK แทน PostgreSQL enum เพราะ ALTER TYPE ทำใน transaction ไม่ได้
ALTER TABLE tenants
  ADD COLUMN plan                 TEXT NOT NULL DEFAULT 'free'
                                  CHECK (plan IN ('free', 'pro', 'scale')),
  ADD COLUMN monthly_quota        INT  NOT NULL DEFAULT 100,
  ADD COLUMN current_period_count INT  NOT NULL DEFAULT 0,
  ADD COLUMN current_period_start DATE NOT NULL DEFAULT CURRENT_DATE;

COMMENT ON COLUMN tenants.current_period_start IS
  'วันเริ่มต้น billing period ปัจจุบัน ใช้สำหรับ reset counter รายเดือน';
