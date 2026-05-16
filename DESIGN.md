---
version: alpha
name: "Ryla — Calm Authority"
description: >
  SaaS integration platform สำหรับร้านค้าออนไลน์ไทย เชื่อม TikTok Shop/Shopee → LINE OA + Google Sheets.
  ออกแบบให้รู้สึก "เชื่อถือได้และเป็นระเบียบ" — เหมือนเครื่องมือมืออาชีพ ไม่ใช่ startup ซุ่มซ่าม

colors:
  # Brand
  primary: "#4F46E5"
  brand: "#4F46E5"
  brand-hover: "#4338CA"
  on-brand: "#FFFFFF"
  # Sidebar (dark surface)
  sidebar: "#0F1117"
  sidebar-border: "#1E2430"
  sidebar-text: "#CBD5E1"
  sidebar-text-muted: "#64748B"
  sidebar-text-active: "#FFFFFF"
  # Page surfaces
  page-bg: "#F8FAFC"
  card: "#FFFFFF"
  # Semantic — Success
  success: "#059669"
  success-bg: "#ECFDF5"
  success-border: "#A7F3D0"
  on-success: "#065F46"
  # Semantic — Error
  error: "#DC2626"
  error-bg: "#FEF2F2"
  error-border: "#FECACA"
  on-error: "#991B1B"
  # Semantic — Warning
  warning: "#D97706"
  warning-bg: "#FFFBEB"
  warning-border: "#FDE68A"
  on-warning: "#92400E"
  # Text
  text-primary: "#0F172A"
  text-secondary: "#475569"
  text-muted: "#94A3B8"
  text-placeholder: "#CBD5E1"
  # Borders
  border-default: "#E2E8F0"
  border-input: "#CBD5E1"
  border-subtle: "#F1F5F9"

typography:
  heading-lg:
    fontFamily: "Geist"
    fontSize: 1.125rem
    fontWeight: 700
    lineHeight: 1.75rem
  heading-md:
    fontFamily: "Geist"
    fontSize: 0.875rem
    fontWeight: 600
    lineHeight: 1.25rem
  body-sm:
    fontFamily: "Geist"
    fontSize: 0.875rem
    fontWeight: 400
    lineHeight: 1.25rem
  label:
    fontFamily: "Geist"
    fontSize: 0.875rem
    fontWeight: 600
    lineHeight: 1.25rem
  caption:
    fontFamily: "Geist"
    fontSize: 0.75rem
    fontWeight: 400
    lineHeight: 1rem
  caption-semibold:
    fontFamily: "Geist"
    fontSize: 0.75rem
    fontWeight: 600
    lineHeight: 1rem
  table-header:
    fontFamily: "Geist"
    fontSize: 0.75rem
    fontWeight: 600
    letterSpacing: 0.05em
  mono:
    fontFamily: "Geist Mono"
    fontSize: 0.75rem
    fontWeight: 400

rounded:
  sm: 6px
  md: 8px
  lg: 12px
  xl: 16px
  full: 9999px

spacing:
  xs: 4px
  sm: 8px
  md: 12px
  lg: 16px
  xl: 24px

components:
  button-primary:
    backgroundColor: "{colors.brand}"
    textColor: "{colors.on-brand}"
    typography: "{typography.body-sm}"
    rounded: "{rounded.sm}"
    padding: "8px 16px"

  button-primary-hover:
    backgroundColor: "{colors.brand-hover}"

  button-secondary:
    backgroundColor: "{colors.card}"
    textColor: "{colors.text-secondary}"
    rounded: "{rounded.sm}"
    padding: "8px 16px"

  button-danger:
    backgroundColor: "{colors.card}"
    textColor: "{colors.error}"
    rounded: "{rounded.sm}"
    padding: "4px 8px"

  button-success-ghost:
    backgroundColor: "{colors.success-bg}"
    textColor: "{colors.on-success}"
    rounded: "{rounded.sm}"
    padding: "4px 12px"

  card:
    backgroundColor: "{colors.card}"
    rounded: "{rounded.md}"
    padding: "16px"

  card-lg:
    backgroundColor: "{colors.card}"
    rounded: "{rounded.lg}"
    padding: "24px"

  input:
    backgroundColor: "{colors.card}"
    textColor: "{colors.text-primary}"
    typography: "{typography.body-sm}"
    rounded: "{rounded.sm}"
    padding: "8px 12px"

  badge-success:
    backgroundColor: "{colors.success-bg}"
    textColor: "{colors.on-success}"
    typography: "{typography.caption-semibold}"
    rounded: "{rounded.full}"
    padding: "2px 8px"

  badge-error:
    backgroundColor: "{colors.error-bg}"
    textColor: "{colors.on-error}"
    typography: "{typography.caption-semibold}"
    rounded: "{rounded.full}"
    padding: "2px 8px"

  badge-warning:
    backgroundColor: "{colors.warning-bg}"
    textColor: "{colors.on-warning}"
    typography: "{typography.caption-semibold}"
    rounded: "{rounded.full}"
    padding: "2px 8px"

  sidebar-nav-item:
    textColor: "{colors.sidebar-text-muted}"
    rounded: "{rounded.sm}"
    padding: "8px 12px"

  sidebar-nav-item-active:
    backgroundColor: "#1B1D23"
    textColor: "{colors.sidebar-text-active}"

  table-header-cell:
    backgroundColor: "{colors.page-bg}"
    textColor: "{colors.text-secondary}"
    typography: "{typography.table-header}"

  alert-success:
    backgroundColor: "{colors.success-bg}"
    textColor: "{colors.on-success}"
    rounded: "{rounded.sm}"
    padding: "12px"

  alert-error:
    backgroundColor: "{colors.error-bg}"
    textColor: "{colors.on-error}"
    rounded: "{rounded.sm}"
    padding: "12px"
---

## Overview

Calm Authority — ออกแบบเพื่อให้ร้านค้าออนไลน์รู้สึกว่า Ryla คือ "เครื่องมือมืออาชีพที่เชื่อถือได้"
ไม่ใช่ prototype หรือ side project

ด้วยพื้นที่ค้าขายออนไลน์ที่วุ่นวายและข้อมูลเยอะ Ryla ต้องการ UI ที่ลด cognitive load:
สี limited, contrast ชัดเจน, hierarchy อ่านง่าย และ state feedback ที่ชัดเจนทันที

สิ่งที่ต้องรู้สึกได้จาก Ryla UI:
- เชื่อถือได้ (reliable) — ไม่มี element ที่ดูไม่แน่ใจ
- เป็นระเบียบ (structured) — hierarchy ชัดทุก page
- ตรงประเด็น (efficient) — ไม่มี decoration ที่ไม่มีหน้าที่

## Colors

Palette มี 3 กลุ่ม:

Brand (Indigo) — `brand` (#4F46E5) คือสีเดียวที่บ่งบอกว่า "คลิกได้" หรือ "ทำงานอยู่"
ใช้เฉพาะ: primary buttons, active nav indicator, focus ring, loading states
ห้ามใช้เป็น background สำหรับ content ทั่วไป

Surface Hierarchy — 3 ชั้นเสมอ:
1. `sidebar` (#0F1117) — dark, anchors the layout
2. `page-bg` (#F8FAFC) — near-white, main canvas
3. `card` (#FFFFFF) — pure white, content surface

Semantic Status — ใช้ตรงตาม intent เท่านั้น:
- `success` (green) → งานสำเร็จ, เชื่อมต่อแล้ว, ยืนยันแล้ว
- `error` (red) → error, ลบ, ยกเลิก
- `warning` (amber) → คำเตือน, action ที่ต้องระวัง
- อย่าใช้ semantic color เพื่อ decoration

## Typography

Font เดียว: Geist (sans) + Geist Mono (code/IDs)
ไม่มี decorative font, ไม่มี mixed font ใน component เดียวกัน

Scale ที่ใช้จริง:
- `heading-lg` — page title เท่านั้น (1 ต่อ page)
- `body-sm` — default body, button text, input text
- `caption` — metadata, help text, table cell content
- `mono` — order IDs, credentials, API keys, code

กฎตัวหนา:
- font-bold (700) → page title, hero text, brand logo
- font-semibold (600) → labels, active nav, section subheaders
- font-medium (500) → button text ขนาดกลาง
- font-normal (400) → body text, help text, table content

## Layout

Dashboard layout: Sidebar (w-56, fixed) + Main (flex-1, overflow-auto, p-6, bg-page-bg)
Sidebar ใช้ `sidebar` (#0F1117) เป็น background เต็ม — ไม่มีสีอื่นใน sidebar surface

Card pattern: Cards ใช้ shadow-sm (subtle depth) + border border-default
ไม่ใช้ shadow-md หรือ shadow-lg ใน dashboard — reserved สำหรับ modals/popovers เท่านั้น

Form layout:
- Max-width: 448px (max-w-md) สำหรับ standalone form
- Label อยู่บน input เสมอ (ไม่ใช้ placeholder-only)
- Help text ต่ำกว่า input, caption typography

Spacing unit: 4px — ทุก spacing ต้องเป็น multiple ของ 4

## Elevation & Depth

3 ระดับเท่านั้น:
- Level 0 — ไม่มี shadow: page background, sidebar
- Level 1 — shadow-sm: cards, form containers, tables
- Level 2 — shadow-md: modals, dropdowns, tooltips (ลอยเหนือ content)

ห้ามใช้ shadow-xl หรือ shadow-2xl ใน dashboard — ใช้ได้เฉพาะ landing page hero

## Shapes

Border radius hierarchy:
- rounded.sm (6px) — inputs, small buttons, pills ขนาดเล็ก
- rounded.md (8px) — standard cards, form containers
- rounded.lg (12px) — larger cards, section containers
- rounded.full — badges, avatar, status dots, tag pills

ใน component เดียวกัน ห้ามผสม radius ขนาดห่างกันเกิน 1 ระดับ
(เช่น card rounded.md ควรมี inner button rounded.sm — ไม่ใช่ rounded.full)

## Components

Buttons:
- Primary: brand background, white text — ใช้ 1 ครั้งต่อ form/section
- Secondary: white background, border-default — cancel หรือ back
- Ghost/Success: success-bg background — inline action บน card
- Danger: white background, error text — delete, disconnect, remove
- Disabled state: opacity-50, cursor-not-allowed เสมอ

Badges:
- ใช้ rounded.full เสมอ
- สีจาก semantic palette: success-bg + on-success, error-bg + on-error, warning-bg + on-warning
- Font: caption-semibold
- ห้ามใช้ badge สีฟ้า (blue) — reserved สำหรับ link/info ที่ไม่ใช่ status

Form Inputs:
- Border: border-input (#CBD5E1) ปกติ, brand focus ring (2px)
- Error state: border-error + alert-error ใต้ input
- Required fields: asterisk (*) สีแดง ต่อท้าย label
- Placeholder: text-placeholder (#CBD5E1) — ห้ามใช้เป็น label แทน

Tables:
- Header row: page-bg background, table-header typography, uppercase
- Data rows: card background, border-subtle (border-b)
- No hover state บน table rows (ทำให้ดูยุ่งเกินไปกับ dense data)

Sidebar Nav:
- Active: border-l-2 brand color, sidebar-nav-item-active background, sidebar-text-active color
- Inactive: sidebar-text-muted, hover sidebar-text
- Nav items: rounded.sm, mx-2 margin

## Do's and Don'ts

Do:
- ใช้ CSS token (--color-brand) แทน hardcoded hex เสมอ
- ใช้ caption + body-sm ที่ถูก type สำหรับทุก text element
- ใช้ semantic color ตรงตาม intent (success เฉพาะ success จริงๆ)
- ใช้ shadow-sm บน card, ไม่มี shadow บน inline elements
- ระบุ disabled:opacity-50 บนทุก interactive element

Don't:
- อย่า hardcode hex ใน inline style หรือ component — reference token เสมอ
- อย่าใช้ text-base (ไม่มีใน scale) — ใช้ body-sm (text-sm)
- อย่าผสม font family ใน component เดียวกัน
- อย่าใช้ shadow-lg, shadow-xl ใน dashboard
- อย่าใช้ rounded-2xl ใน dashboard — เฉพาะ landing page
- อย่าสร้าง status color ใหม่นอกจาก success/error/warning ที่กำหนดไว้
