# RYLA-6: Flow Configuration UI — Design Spec

**Date:** 2026-04-25
**Status:** Approved
**Author:** Brainstorming session

---

## Context

Backend v0.2.0 เสร็จแล้ว — Shopee/TikTok webhook receiver, LINE OA push, Google Sheets adapter ทำงานได้จริง (validated กับ real APIs แล้ว) แต่ยังไม่มี UI ให้ user ตั้งค่า

RYLA-6 สร้าง frontend MVP ที่ให้ seller เชื่อมต่อ LINE OA + Google Sheets และทดสอบ integration ได้ทันที โดยไม่ต้องรอ Shopee real credentials (เพราะ Shopee ต้องมี business registration ก่อน)

**Goal:** User signup → connect LINE → กดทดสอบ → รับ LINE message ภายใน 5 นาที

---

## Scope (ใน RYLA-6)

| Feature | อยู่ใน scope |
|---------|------------|
| Google OAuth login/logout | ✅ |
| Dashboard layout (sidebar + main) | ✅ |
| Connections list page | ✅ |
| Add LINE OA connection form | ✅ |
| Add Google Sheets connection form (drag & drop JSON) | ✅ |
| Delete connection | ✅ |
| "ส่งทดสอบ" button (LINE) | ✅ |
| Webhook URL display page | ✅ |
| Middleware: redirect ถ้าไม่ได้ login | ✅ |
| Shopee / TikTok connection UI | ❌ (defer) |
| Flow rules / mapping UI | ❌ (defer) |
| Activity log / history | ❌ (defer) |

---

## Architecture Decision

**Hybrid approach:**
- Frontend เขียน credentials ตรงใน Supabase (`connections` table) ผ่าน `@supabase/ssr` — RLS ป้องกัน tenant isolation
- ปุ่ม "ส่งทดสอบ" เรียก Next.js API route → proxy ไปยัง .NET backend endpoint ใหม่ `POST /api/test-notification`
- ไม่สร้าง backend CRUD endpoints ใหม่ — ลด scope และ AOT complexity

---

## Route Structure

```
frontend/src/app/
├── layout.tsx                          # root layout (fonts, metadata)
├── page.tsx                            # Landing page (มีอยู่แล้ว — ปรับ CTA)
├── middleware.ts                       # protect (dashboard) routes
│
├── (auth)/
│   ├── login/
│   │   └── page.tsx                   # "Sign in with Google" page
│   └── auth/
│       └── callback/
│           └── route.ts               # Supabase OAuth callback handler
│
└── (dashboard)/
    ├── layout.tsx                      # sidebar + topbar layout
    ├── connections/
    │   ├── page.tsx                    # connections list (หน้าหลัก)
    │   └── new/
    │       └── [platform]/
    │           └── page.tsx           # form เพิ่ม connection (line | google-sheets)
    └── webhook/
        └── page.tsx                   # แสดง webhook URL + copy button
```

---

## Pages Detail

### `/login`
- ปุ่มเดียว: **"Sign in with Google"** — ใช้ Supabase `signInWithOAuth({ provider: 'google' })`
- หลัง callback redirect → `/connections`
- ถ้า login แล้วและเข้า `/login` → redirect `/connections`

### `/connections` (หน้าหลัก)
- แสดง connection card แต่ละอัน (LINE OA, Google Sheets, Shopee disabled)
- แต่ละ card: platform icon + status badge (Connected / ยังไม่เชื่อมต่อ) + actions
- LINE card actions: **ส่งทดสอบ** | แก้ไข | ลบ
- Google Sheets card actions: **เปิด Spreadsheet** | แก้ไข | ลบ
- Shopee card: disabled state พร้อม tooltip "ต้องมี Partner Key"
- ปุ่ม "+ เพิ่ม Connection" → redirect ไปหน้า platform picker (ไม่ใช้ modal — เรียบง่ายกว่า)
- ปุ่ม "แก้ไข" → redirect ไปยัง `/connections/new/[platform]` พร้อม pre-filled data จาก existing connection

### `/connections/new/[platform]`
- `platform = "line"` → LINE OA form
- `platform = "google-sheets"` → Google Sheets form

**LINE OA Form fields:**
| Field | Required | Hint |
|-------|----------|------|
| Channel Access Token | ✅ | LINE Developers Console → Messaging API tab → Channel access token (long-lived) |
| Target User ID | ✅ | LINE OA Manager → Basic settings → Your user ID |

**Google Sheets Form fields:**
| Field | Required | Note |
|-------|----------|------|
| Service Account JSON | ✅ | Drag & drop .json file จาก Google Cloud — parse client-side |
| Spreadsheet ID | ✅ | จาก URL ระหว่าง `/d/` และ `/edit` |
| Sheet Tab Name | optional | Default: `Orders` |

### `/webhook`
- แสดง webhook URL สำหรับ Shopee: `https://<domain>/webhooks/shopee`
- ปุ่ม Copy to clipboard
- คำอธิบาย: วิธีนำ URL ไปใส่ใน Shopee Open Platform

---

## Data Flow

### Save LINE connection
```
User กรอก form
→ validate client-side (field ไม่ว่าง)
→ supabaseClient.from('connections').upsert({
    tenant_id: user.id,
    platform: 'line_oa',
    credentials: { channel_access_token, target_user_id },
    is_active: true
  })
→ redirect /connections
```

### Save Google Sheets connection
```
User drag & drop JSON file
→ FileReader.readAsText() → JSON.parse()
→ ดึงเฉพาะ { type, client_email, private_key, token_uri }
→ validate: ต้องมี client_email และ private_key
→ supabaseClient.from('connections').upsert({
    tenant_id: user.id,
    platform: 'google_sheets',
    credentials: {
      spreadsheet_id,
      worksheet_name,
      service_account_json: JSON.stringify(serviceAccountData)
    },
    is_active: true
  })
→ redirect /connections
```

### ปุ่ม "ส่งทดสอบ" (LINE)
```
User กด button
→ fetch('/api/test-notification', { method: 'POST' })
  (Next.js API route — ส่ง Supabase session cookie ไปด้วย)
→ API route ดึง tenant_id จาก session
→ POST http://backend/api/test-notification { tenantId }  ← .NET endpoint ใหม่
→ backend ส่ง LINE message พร้อม mock order data
→ แสดง toast: "✅ ส่งสำเร็จ! เช็ค LINE ของคุณ"
```

---

## Backend Change (เพิ่มใหม่ 1 endpoint)

`POST /api/test-notification`

```
Request: { tenantId: string }
Auth: Supabase JWT (validate ใน middleware)

Flow:
1. ดึง LINE credentials จาก connections table
2. สร้าง mock OrderWebhookContext (platform=Shopee, orderId="TEST-xxx")
3. เรียก lineNotifier.PushAsync(...)
4. คืน { success: bool, message: string }
```

---

## Component Structure

```
frontend/src/
├── app/                          # pages (ตาม routes)
├── components/
│   ├── ui/                       # atomic: Button, Input, Badge, Toast
│   ├── layout/
│   │   ├── Sidebar.tsx
│   │   └── TopBar.tsx
│   └── connections/
│       ├── ConnectionCard.tsx    # card แสดง connection + actions
│       ├── LineForm.tsx          # form เพิ่ม LINE OA
│       └── SheetsForm.tsx        # form เพิ่ม Sheets (รวม drag & drop)
└── lib/
    ├── supabase/                 # client + server (มีอยู่แล้ว)
    └── connections.ts            # helper: getConnections, upsertConnection, deleteConnection
```

---

## Supabase RLS Policy ที่ต้องมี

```sql
-- connections table — tenant isolation
CREATE POLICY "Users can only see own connections"
  ON connections FOR SELECT
  USING (tenant_id = auth.uid());

CREATE POLICY "Users can upsert own connections"
  ON connections FOR INSERT
  WITH CHECK (tenant_id = auth.uid());

CREATE POLICY "Users can update own connections"
  ON connections FOR UPDATE
  USING (tenant_id = auth.uid());

CREATE POLICY "Users can delete own connections"
  ON connections FOR DELETE
  USING (tenant_id = auth.uid());
```

**MVP Decision:** `tenant_id = auth.uid()` — Supabase Auth user ID ใช้เป็น tenant ID โดยตรง ไม่มี tenants table แยก ทำให้ RLS ทำงานได้ทันทีโดยไม่ต้องมี mapping table

---

## UI Library

ใช้ **Tailwind CSS 4** (มีอยู่แล้ว) + components เขียนเอง — ไม่เพิ่ม library ใหม่ (เพื่อ keep bundle เล็ก)

Toast notification: ใช้ `useState` + CSS transition เรียบง่าย

---

## Testing

- **Unit tests (Vitest):** ConnectionCard, LineForm validation, SheetsForm JSON parse
- **E2E:** ไม่มีใน scope นี้ (frontend E2E ใช้ Playwright — defer)
- **Manual verification:** login → add LINE → send test → receive LINE message

---

## Success Criteria

1. User login ด้วย Google ได้
2. เพิ่ม LINE connection ได้ (form save → card แสดง Connected)
3. กด "ส่งทดสอบ" → ได้ LINE message จริง
4. เพิ่ม Google Sheets connection ได้ (drag & drop JSON file)
5. ลบ connection ได้
6. Middleware redirect ถ้าไม่ได้ login
