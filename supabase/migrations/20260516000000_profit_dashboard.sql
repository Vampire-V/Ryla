-- =============================================================
-- Ryla: Real Profit Dashboard — RYLA-9
-- =============================================================
-- 4 tables ใหม่: orders, order_items, sku_costs, shopee_tokens
-- กฎ: RLS เปิดทุก table เสมอ
-- Financial fields nullable จนกว่า status = COMPLETED + synced

-- -------------------------------------------------------------
-- Orders: 1 row ต่อ 1 order — webhook สร้าง pending, Function fills financial
-- -------------------------------------------------------------
CREATE TABLE orders (
  id               UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  tenant_id        UUID NOT NULL REFERENCES tenants(id) ON DELETE CASCADE,
  platform         TEXT NOT NULL CHECK (platform IN ('shopee')),
  order_sn         TEXT NOT NULL,
  status           TEXT NOT NULL,
  currency         TEXT NOT NULL DEFAULT 'THB' CHECK (currency = 'THB'),
  -- financial fields: NULL จนกว่า status = COMPLETED และ sync เสร็จ
  revenue          NUMERIC(12,2),
  commission       NUMERIC(12,2),
  shipping_rebate  NUMERIC(12,2),
  voucher_deduct   NUMERIC(12,2),
  gross_margin     NUMERIC(12,2),
  net_margin       NUMERIC(12,2),
  sync_status      TEXT NOT NULL DEFAULT 'pending'
                   CHECK (sync_status IN ('pending', 'synced', 'error', 'auth_error')),
  sync_error       TEXT,
  synced_at        TIMESTAMPTZ,
  order_created_at TIMESTAMPTZ,
  received_at      TIMESTAMPTZ NOT NULL DEFAULT now(),
  updated_at       TIMESTAMPTZ NOT NULL DEFAULT now(),
  UNIQUE (tenant_id, platform, order_sn)
);

ALTER TABLE orders ENABLE ROW LEVEL SECURITY;

CREATE POLICY "orders: tenant isolation"
  ON orders FOR ALL
  USING (
    tenant_id = (SELECT tenant_id FROM profiles WHERE id = auth.uid())
  );

CREATE INDEX idx_orders_tenant_created ON orders(tenant_id, order_created_at DESC);
CREATE INDEX idx_orders_sync_pending ON orders(sync_status) WHERE sync_status = 'pending';

-- -------------------------------------------------------------
-- Order Items: ของในแต่ละ order (สำหรับคำนวณ net_margin ด้วย sku_costs)
-- tenant_id denormalized เพื่อ RLS performance
-- -------------------------------------------------------------
CREATE TABLE order_items (
  id          UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  order_id    UUID NOT NULL REFERENCES orders(id) ON DELETE CASCADE,
  tenant_id   UUID NOT NULL REFERENCES tenants(id) ON DELETE CASCADE,
  item_sku    TEXT NOT NULL,
  model_sku   TEXT,
  item_name   TEXT,
  quantity    INT NOT NULL DEFAULT 1 CHECK (quantity > 0),
  item_price  NUMERIC(12,2)
);

ALTER TABLE order_items ENABLE ROW LEVEL SECURITY;

CREATE POLICY "order_items: tenant isolation"
  ON order_items FOR ALL
  USING (
    tenant_id = (SELECT tenant_id FROM profiles WHERE id = auth.uid())
  );

CREATE INDEX idx_order_items_order ON order_items(order_id);
CREATE INDEX idx_order_items_tenant_sku ON order_items(tenant_id, item_sku);

-- -------------------------------------------------------------
-- SKU Costs: COGS ที่ user กรอกเอง — parent SKU level (MVP)
-- หมายเหตุ: 1 cost ต่อ item_sku ไม่แยก variant (model_sku) — v2 feature
-- -------------------------------------------------------------
CREATE TABLE sku_costs (
  id          UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  tenant_id   UUID NOT NULL REFERENCES tenants(id) ON DELETE CASCADE,
  platform    TEXT NOT NULL CHECK (platform IN ('shopee')),
  item_sku    TEXT NOT NULL,
  item_name   TEXT,
  cogs        NUMERIC(12,2) NOT NULL CHECK (cogs > 0),
  created_at  TIMESTAMPTZ NOT NULL DEFAULT now(),
  updated_at  TIMESTAMPTZ NOT NULL DEFAULT now(),
  UNIQUE (tenant_id, platform, item_sku)
);

ALTER TABLE sku_costs ENABLE ROW LEVEL SECURITY;

CREATE POLICY "sku_costs: tenant isolation"
  ON sku_costs FOR ALL
  USING (
    tenant_id = (SELECT tenant_id FROM profiles WHERE id = auth.uid())
  );

CREATE INDEX idx_sku_costs_tenant_sku ON sku_costs(tenant_id, platform, item_sku);

-- -------------------------------------------------------------
-- Shopee Tokens: OAuth tokens สำหรับ Finance API polling
-- แยกจาก connections table (connections = webhook config)
-- เก็บ Supabase Vault secret_id ไม่ใช่ plaintext token
-- -------------------------------------------------------------
CREATE TABLE shopee_tokens (
  id                      UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  tenant_id               UUID NOT NULL UNIQUE REFERENCES tenants(id) ON DELETE CASCADE,
  shop_id                 BIGINT NOT NULL,
  access_token_secret_id  UUID NOT NULL,
  refresh_token_secret_id UUID NOT NULL,
  expires_at              TIMESTAMPTZ NOT NULL,
  created_at              TIMESTAMPTZ NOT NULL DEFAULT now(),
  updated_at              TIMESTAMPTZ NOT NULL DEFAULT now()
);

ALTER TABLE shopee_tokens ENABLE ROW LEVEL SECURITY;

CREATE POLICY "shopee_tokens: tenant isolation"
  ON shopee_tokens FOR ALL
  USING (
    tenant_id = (SELECT tenant_id FROM profiles WHERE id = auth.uid())
  );
