# MCP Server Setup — Ryla

MCP (Model Context Protocol) servers ที่ configure ไว้ใน `.claude/settings.json`

---

## 1. Supabase — Database Management

**ใช้สำหรับ:** สร้าง/แก้ไข tables, migrations, RLS policies, ดู data, จัดการ auth users

```bash
# ติดตั้ง: ไม่ต้องติดตั้ง — ใช้ npx โดยตรง
# ต้องการ environment variables:
export SUPABASE_ACCESS_TOKEN="<personal access token>"
# ดูที่: supabase.com → Account → Access Tokens → Generate new token

export SUPABASE_PROJECT_REF="<project ref>"
# ดูที่: supabase.com → Project → Settings → General → Reference ID
```

**ความสามารถ:**
- Query/update tables โดยตรงผ่าน SQL
- สร้าง migration files
- จัดการ RLS policies
- ดู auth logs

---

## 2. PostgreSQL — Direct Database Access

**ใช้สำหรับ:** Query database โดยตรง (local dev หรือ production read-only)

```bash
export POSTGRES_CONNECTION_STRING="postgresql://user:password@host:5432/dbname"

# สำหรับ Supabase local dev:
export POSTGRES_CONNECTION_STRING="postgresql://postgres:postgres@localhost:54322/postgres"

# สำหรับ Supabase production (ใช้ connection pooler):
export POSTGRES_CONNECTION_STRING="postgresql://postgres.[PROJECT_REF]:[PASSWORD]@aws-0-ap-southeast-1.pooler.supabase.com:6543/postgres"
```

**ความสามารถ:**
- Raw SQL queries
- Schema inspection
- Performance analysis
- ดู execution plans

---

## 3. Azure — Cloud Infrastructure

**ใช้สำหรับ:** จัดการ Azure Functions, Key Vault, Storage, App Settings

```bash
# ต้องทำ Azure CLI login ก่อน:
az login

export AZURE_SUBSCRIPTION_ID="<subscription id>"
# ดูที่: portal.azure.com → Subscriptions

export AZURE_RESOURCE_GROUP="ryla-prod"
# ชื่อ Resource Group ที่สร้างสำหรับ Ryla
```

**ความสามารถ:**
- Deploy Azure Functions
- จัดการ App Settings / environment variables
- ดู logs จาก Application Insights
- สร้าง/จัดการ Key Vault secrets

---

## 4. Brave Search — Web Research

**ใช้สำหรับ:** ค้นหา .NET 10 documentation, LINE API, TikTok Shop API, Shopee API

```bash
export BRAVE_API_KEY="<api key>"
# สมัครที่: brave.com/search/api → Free tier: 2,000 queries/month
```

**ใช้เมื่อ:** ต้องการ up-to-date docs ที่ไม่อยู่ใน training data

---

## 5. Context7 — Library Documentation

**ใช้สำหรับ:** ดึง documentation ล่าสุดของ libraries (.NET, Next.js, Supabase, etc.)

```bash
# ไม่ต้องการ API key — ใช้ได้เลย
# Context7 รองรับ: .NET, ASP.NET Core, Next.js, React, Supabase, และอื่นๆ
```

**ใช้เมื่อ:** ต้องการ API reference ที่แม่นยำสำหรับ .NET 10, Next.js 15, หรือ Supabase SDK

**ตัวอย่าง prompt:**
```
use context7 to find the correct syntax for WebApplication.CreateSlimBuilder in .NET 10
```

---

## การ Activate MCP Servers

### วิธีที่ 1: Export ใน shell profile (แนะนำ)

เพิ่มใน `~/.zshrc`:
```bash
# Ryla MCP Server Keys
export SUPABASE_ACCESS_TOKEN="..."
export SUPABASE_PROJECT_REF="..."
export POSTGRES_CONNECTION_STRING="..."
export AZURE_SUBSCRIPTION_ID="..."
export AZURE_RESOURCE_GROUP="ryla-prod"
export BRAVE_API_KEY="..."
```

แล้ว restart Claude Code

### วิธีที่ 2: `.env.local` (ไม่ commit)

สร้างไฟล์ `.env.local` ที่ root ของ project (อยู่ใน `.gitignore` แล้ว):
```bash
SUPABASE_ACCESS_TOKEN=...
SUPABASE_PROJECT_REF=...
POSTGRES_CONNECTION_STRING=...
```

---

## ตรวจสอบว่า MCP ทำงาน

หลัง restart Claude Code ให้ลองพิมพ์:
```
/mcp
```
จะเห็น list ของ MCP servers ที่ active พร้อม tools ที่ใช้ได้
