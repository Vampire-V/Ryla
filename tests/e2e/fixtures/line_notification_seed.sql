-- E2E seed for RYLA-4 LINE notification tests (full mode)
-- รัน: psql "$DATABASE_URL" -f tests/e2e/fixtures/line_notification_seed.sql
-- หรือผ่าน Supabase CLI: supabase db reset (ถ้าใส่เป็น seed.sql ถาวร)
--
-- สร้าง tenant ตัวอย่าง + connections ของ TikTok, Shopee, LINE OA
-- ใช้ shop_id fixed เพื่อให้ test script query ได้ตามที่ hardcode ไว้

BEGIN;

-- Clean leftover data from previous E2E runs (safe idempotent)
DELETE FROM public.connections WHERE tenant_id IN (
    SELECT id FROM public.tenants WHERE name = 'E2E Test Tenant'
);
DELETE FROM public.tenants WHERE name = 'E2E Test Tenant';

-- Test tenant
INSERT INTO public.tenants (id, name)
VALUES ('11111111-1111-1111-1111-111111111111', 'E2E Test Tenant');

-- TikTok Shop connection (shop_id matches SEEDED_TIKTOK_SHOP_ID env default)
INSERT INTO public.connections (tenant_id, platform, credentials, is_active)
VALUES (
    '11111111-1111-1111-1111-111111111111',
    'tiktok_shop',
    '{"shop_id": "7010000000001"}'::jsonb,
    true
);

-- Shopee connection (shop_id matches SEEDED_SHOPEE_SHOP_ID env default)
INSERT INTO public.connections (tenant_id, platform, credentials, is_active)
VALUES (
    '11111111-1111-1111-1111-111111111111',
    'shopee',
    '{"shop_id": "900001"}'::jsonb,
    true
);

-- LINE OA connection — dummy token (LINE stub accepts anything)
INSERT INTO public.connections (tenant_id, platform, credentials, is_active)
VALUES (
    '11111111-1111-1111-1111-111111111111',
    'line_oa',
    '{"channel_access_token": "stub-token-e2e", "target_user_id": "Ue2e-test-user-id"}'::jsonb,
    true
);

COMMIT;
