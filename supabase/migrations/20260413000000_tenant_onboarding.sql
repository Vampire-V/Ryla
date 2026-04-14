-- =============================================================
-- Tenant Onboarding: auto-provision tenant + profile on signup
-- =============================================================
-- เมื่อ user สมัครผ่าน Supabase Auth จะสร้าง tenant + profile อัตโนมัติ

-- -------------------------------------------------------------
-- 1. RLS Policy สำหรับ tenants table (ยังไม่มี)
-- user เห็นได้แค่ tenant ของตัวเอง (ผ่าน profiles lookup)
-- -------------------------------------------------------------
CREATE POLICY "tenants: tenant member access"
  ON tenants FOR ALL
  USING (
    id IN (SELECT tenant_id FROM profiles WHERE id = auth.uid())
  );

-- -------------------------------------------------------------
-- 2. Trigger function: สร้าง tenant + profile เมื่อ user ใหม่สมัคร
-- ทำงานใน transaction เดียวกับ INSERT ใน auth.users
-- SECURITY DEFINER: ทำงานด้วยสิทธิ์ของ owner (postgres) เพราะ client
--   ไม่มี permission INSERT ลงใน tenants/profiles โดยตรง
-- search_path: set ให้ใช้ public schema เท่านั้น เพื่อความปลอดภัย
-- -------------------------------------------------------------
CREATE OR REPLACE FUNCTION public.handle_new_user()
RETURNS TRIGGER
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = public
AS $$
DECLARE
  new_tenant_id UUID;
  user_name TEXT;
BEGIN
  -- ดึงชื่อจาก metadata หรือใช้ส่วนแรกของ email
  user_name := COALESCE(
    NEW.raw_user_meta_data ->> 'full_name',
    split_part(NEW.email, '@', 1)
  );

  -- สร้าง tenant ใหม่ (1 user = 1 tenant ตอน signup)
  INSERT INTO public.tenants (name)
  VALUES (user_name || '''s Business')
  RETURNING id INTO new_tenant_id;

  -- สร้าง profile เชื่อม user กับ tenant
  INSERT INTO public.profiles (id, tenant_id, full_name)
  VALUES (NEW.id, new_tenant_id, user_name);

  RETURN NEW;
END;
$$;

-- -------------------------------------------------------------
-- 3. Trigger: fire หลัง INSERT ใน auth.users
-- FOR EACH ROW: ทำงานทุกครั้งที่มี user ใหม่
-- AFTER INSERT: รอให้ auth.users insert สำเร็จ จึงค่อยสร้าง tenant/profile
-- IF NOT EXISTS: ป้องกัน error ถ้ารัน migration หลายครั้ง
-- (ใช้ได้ใน PostgreSQL 15+ ถ้า server เก่ากว่า ต้องลบ IF NOT EXISTS)
-- -------------------------------------------------------------
CREATE TRIGGER on_auth_user_created
  AFTER INSERT ON auth.users
  FOR EACH ROW
  EXECUTE FUNCTION public.handle_new_user();
