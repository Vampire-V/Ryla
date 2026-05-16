# Ryla Real Profit Dashboard — Design Spec

**Date:** 2026-05-16  
**Feature:** RYLA-9 Real Profit Dashboard  
**Status:** Approved — ready for implementation

---

## Why

Community feedback จากกลุ่ม Facebook ร้านค้า TikTok Shop/Shopee ไทย: ร้านค้าไม่รู้ margin จริงหลังหักค่า commission, voucher, และค่าจัดส่งของ Shopee ทำให้ repricing ผิดพลาด Financial pain มี WTP สูงเพราะกระทบรายได้โดยตรง

**Goal:** Dashboard แสดง gross margin และ net margin จริงต่อ order

---

## Key Decisions

| # | Decision | Reason |
|---|---|---|
| 1 | Shopee-only MVP | TikTok ต้องการ Business Partner account |
| 2 | Hybrid: webhook → pending → Azure Function poll | Shopee webhook ไม่มีข้อมูลการเงิน |
| 3 | `get_order_detail` เฉพาะ COMPLETED | escrow_* ยังไม่ final ก่อน buyer confirm |
| 4 | Show all + "รอยืนยัน" badge | KPI นับ COMPLETED; table แสดงทุก order |
| 5 | `connections` vs `shopee_tokens` แยก | webhook config vs OAuth Finance API |
| 6 | COALESCE upsert | webhook (NULL) ต้องไม่ overwrite synced data |
| 7 | `access_token_secret_id UUID` | เก็บ Vault secret_id ไม่ใช่ plaintext |
| 8 | item_sku parent level | variant COGS → v2 |
| 9 | `Ryla.Functions` project ใหม่ | Functions.Worker v1.22+ AOT-compatible |
| 10 | 30-day backfill เท่านั้น | ป้องกัน rate limit blast |
| 11 | THB only | multi-currency นอก scope |

---

## Architecture

```
[Shopee webhook] → Ryla.Api (existing RYLA-3)
  → INSERT orders(order_sn, status, sync_status='pending')

[Azure Function Timer, ทุก 1 ชม.] ← Ryla.Functions (NEW)
  → query pending COMPLETED orders
  → call get_order_detail → upsert financials
  → refresh access_token ถ้า expiry < 30 min

[Next.js] → GET /api/profit/* → Ryla.Api → Ryla.Core → Ryla.Infrastructure
```

**New project:** `backend/src/Ryla.Functions/` (`Microsoft.NET.Sdk.FunctionWorker`, AOT)  
**Dependency:** Ryla.Functions → Ryla.Core ← Ryla.Infrastructure (shared, AOT-safe)

---

## Data Model

4 new tables:

### `orders`
- `(tenant_id, platform, order_sn)` UNIQUE
- Financial fields (revenue, commission, shipping_rebate, voucher_deduct, gross_margin, net_margin) nullable — populated only when `status = COMPLETED` + synced
- `sync_status`: pending | synced | error | auth_error
- `currency TEXT DEFAULT 'THB' CHECK (currency = 'THB')`

### `order_items`
- Linked to `orders(id)`
- `item_sku` (parent), `model_sku` (variant, nullable), `quantity`, `item_price`
- Used for net_margin: Σ(sku_cost.cogs × quantity)

### `sku_costs`
- User-entered COGS per `(tenant_id, platform, item_sku)`
- MVP: parent SKU only — one cost per item regardless of variant

### `shopee_tokens`
- `access_token_secret_id UUID` + `refresh_token_secret_id UUID` → Supabase Vault pointers
- `UNIQUE(tenant_id)` — 1 Shopee shop per tenant (MVP)

**RLS pattern (all tables):**
```sql
USING (tenant_id = (SELECT tenant_id FROM profiles WHERE id = auth.uid()))
```

**Upsert semantics:**
```sql
ON CONFLICT DO UPDATE SET
  status = EXCLUDED.status,
  revenue = COALESCE(EXCLUDED.revenue, orders.revenue),
  -- financial: prefer incoming if not null, keep existing otherwise
  updated_at = now()
```

---

## Product Experience

**3-step onboarding wizard** (`/settings/connections/shopee`):
1. Connect — OAuth (scope: `shop.basic.info` + `order.read`, read-only)
2. Sync — progress bar polling `/api/profit/sync-status` → "กำลัง sync ย้อนหลัง 30 วัน..."
3. COGS — SKU list from synced orders + cost input

**Dashboard** (`/dashboard/profit`):
- KPI cards: Revenue, Gross Margin %, Net Margin % (COMPLETED only + note)
- Orders table: all statuses; "รอยืนยัน" badge (amber) for non-COMPLETED; NULL = "—"
- Date range: 7 / 30 / 90 days or custom

**Empty states:** CTA before connect; skeleton + progress after connect

---

## MVP Scope

**In:** Shopee OAuth, 30-day backfill, hourly sync, KPI dashboard, orders table, COGS input, date range, sync status, empty states

**Out (v2+):** TikTok, variant COGS, CSV export, multi-shop, LINE margin alert, campaign ROI, multi-currency

---

## API Endpoints (New)

- `GET /api/profit/summary?range=30d`
- `GET /api/profit/orders?range=30d&page=1&limit=50`
- `GET /api/profit/sku-costs`
- `POST /api/profit/sku-costs`
- `GET /api/shopee/oauth/authorize`
- `GET /api/shopee/oauth/callback`
- `GET /api/profit/sync-status`

---

## Testing

**Unit (Ryla.Core.Tests):** ComputeGrossMargin, ComputeNetMargin, SyncOrder (COMPLETED only), RefreshToken, ParseOrderDetail

**Integration (Testcontainers):** OrderRepository upsert COALESCE, ShopeeTokenAdapter Vault round-trip, RLS cross-tenant isolation

**Frontend (Vitest + RTL):** badge, KPI filter, form validation, empty states

**E2E (`tests/e2e/profit-dashboard.py`, 11 cases):** happy path, pending badge, KPI COMPLETED-only, auth_error, no-COGS dash, webhook/backfill race, model_sku fallback, signature tamper, cross-tenant RLS

---

## Implementation Plan

See `/docs/superpowers/plans/` or the approved plan file for task breakdown (8 tasks).  
Branch: `feat/RYLA-9-profit-dashboard` → PR to `develop`
