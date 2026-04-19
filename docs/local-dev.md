# Local Development Setup

World-class local development workflow — พัฒนาและทดสอบ Ryla ได้โดยไม่ต้องใช้อินเทอร์เน็ต

---

## Prerequisites

```bash
# ตรวจสอบ tools ที่ต้องการ
dotnet --version   # >= 10.0
node --version     # >= 22
docker --version   # Desktop หรือ Engine (สำหรับ Supabase + Testcontainers)
supabase --version # >= 2.0 (brew install supabase/tap/supabase)
```

---

## ครั้งแรก (First-time Setup)

```bash
# 1. Setup — ตรวจสอบ prerequisites (Docker, Supabase CLI) + ติดตั้ง deps + git hooks
make setup

# 2. เริ่ม dev stack (Supabase + DB ready check)
make dev

# 3. Apply migrations + seed data (ครั้งแรกเท่านั้น)
make db-reset

# 4. อัปเดต frontend/.env.local ด้วยค่าจาก 'supabase status'
supabase status
# ANON KEY และ API URL จะแสดงที่นี่
```

---

## Daily Development

```bash
# Terminal 1: เริ่ม dev stack (ครั้งเดียวต่อ session)
make dev
# → ตรวจสอบ Docker + Supabase CLI
# → เริ่ม Supabase stack
# → รอ DB ready ที่ localhost:54322
# → แสดงข้อความ "Dev stack ready" พร้อม next steps

# Terminal 2: รัน .NET backend
dotnet run --project backend/src/Ryla.Api/Ryla.Api.csproj --launch-profile http
# → http://localhost:5282
# → http://localhost:5282/health     (เช็ค DB connectivity ด้วย)
# → http://localhost:5282/openapi/v1.json
#
# ⚠️  Backend fail-fast ถ้า DB ไม่พร้อม — ต้อง make dev ก่อนเสมอ

# Terminal 3: รัน Next.js frontend
npm run dev --prefix frontend
# → http://localhost:3000
```

### Fail-fast at startup

ตั้งแต่ DX-01 app จะ **exit immediately** ถ้าเชื่อม DB ไม่ได้ตอน startup:

```
fail: Ryla.Infrastructure.Adapters.Database.DbStartupProbe[0]
      Database startup probe FAILED. Fix: 'make dev-start' (local dev) ...
Unhandled exception. InvalidOperationException: Database unreachable at startup.
```

ไม่ใช่ bug — เป็น design ตั้งใจ: ถ้า DB ไม่มี, webhook ทำงานไม่ได้จริง
การ fail-fast ดีกว่าสร้าง false "healthy" signal

Override timeout (default 5s) ด้วย env var:
```bash
STARTUP_DB_PROBE_TIMEOUT_SECONDS=10 dotnet run ...
```

---

## Database Workflow

```bash
# สร้าง migration ใหม่ (หลัง edit schema ใน Supabase Studio)
make db-migrate name=add_flows_table
# → สร้าง supabase/migrations/YYYYMMDDHHMMSS_add_flows_table.sql

# หรือ auto-generate จาก schema diff
make db-diff name=add_flows_table
# → supabase เปรียบเทียบ local DB กับ migrations แล้วสร้าง migration

# Reset database (migrations + seed)
make db-reset

# Generate TypeScript types หลัง schema เปลี่ยน
make types
# → อัปเดต frontend/src/types/database.types.ts อัตโนมัติ
```

---

## Testing

```bash
# Unit tests เท่านั้น (เร็ว, ไม่ต้องการ Docker)
make test-unit

# Integration tests (ต้องมี Docker running)
make test-infra

# ทั้งหมด
make test

# พร้อม coverage report
make test-coverage
```

### Integration Tests (Testcontainers)

`Ryla.Infrastructure.Tests` ใช้ Testcontainers เพื่อ:
1. รัน PostgreSQL container อัตโนมัติ (ไม่ต้องการ Supabase local)
2. Apply migrations ทั้งหมดจาก `supabase/migrations/`
3. ทดสอบ adapters กับ database จริง

```bash
# ต้องการ Docker เท่านั้น
make test-infra
```

---

## Architecture: Local vs Production

```
LOCAL DEV                          PRODUCTION
─────────────────────────────────  ─────────────────────────────────
Supabase local (Docker)            Supabase Cloud
  localhost:54321 (API)              https://xxx.supabase.co
  localhost:54322 (PostgreSQL)       Managed PostgreSQL
  localhost:54323 (Studio)

appsettings.Development.json       Azure Key Vault / App Settings
  ConnectionStrings:Supabase         Encrypted secrets
  (localhost credentials)

dotnet run                         Azure Functions (Native AOT)
  ASPNETCORE_ENVIRONMENT=Development  linux-x64 binary

npm run dev                        Vercel / Cloudflare Pages
  localhost:3000                     Edge deployment
```

---

## Environment Variables Reference

### frontend/.env.local (ไม่ commit — อยู่ใน .gitignore)

```bash
NEXT_PUBLIC_SUPABASE_URL=http://localhost:54321
NEXT_PUBLIC_SUPABASE_ANON_KEY=<ค่าจาก 'supabase status'>
```

### backend (appsettings.Development.json — อยู่ใน .gitignore)

```json
{
  "ConnectionStrings": {
    "Supabase": "Host=localhost;Port=54322;Database=postgres;Username=postgres;Password=postgres;"
  }
}
```

หรือใช้ dotnet user-secrets (แนะนำ):
```bash
dotnet user-secrets set "ConnectionStrings:Supabase" \
  "Host=localhost;Port=54322;Database=postgres;Username=postgres;Password=postgres;" \
  --project backend/src/Ryla.Api
```

---

## Troubleshooting

| ปัญหา | สาเหตุ | วิธีแก้ |
|---|---|---|
| `supabase: command not found` | ยังไม่ได้ติดตั้ง | `brew install supabase/tap/supabase` |
| Port 54321/54322 ถูกใช้อยู่ | app อื่นใช้ port | `lsof -i :54322` แล้ว kill, หรือ `make dev-stop` |
| `Docker daemon is not running` | Docker Desktop ปิดอยู่ | เปิด Docker Desktop ก่อน (`make dev` จะเตือน) |
| **Backend exit ทันทีด้วย "Database unreachable"** | DB ไม่ได้เปิด | `make dev` ก่อน (ไม่ใช่ bug — fail-fast by design) |
| `make dev` ค้างที่ "Waiting for DB" | Container ยังไม่ boot | รอ 30s, ถ้าเกินให้ `make dev-stop && make dev` |
| Migration fail | SQL syntax error | ดู `supabase db reset` output |
| Testcontainers timeout | Docker slow | เพิ่ม timeout ใน `PostgreSqlBuilder.WithWaitStrategy` |
| Types outdated | Schema เปลี่ยนแล้ว | `make types` |
| `/health` returns 503 | DB ล่มตอน runtime | `docker ps` เช็ค supabase_db_ryla, restart ถ้าต้อง |
