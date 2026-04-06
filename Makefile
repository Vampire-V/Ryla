# ─────────────────────────────────────────────────────────────────────────────
# Ryla — Developer Commands
# ─────────────────────────────────────────────────────────────────────────────
# ใช้: make <command>
# ดูคำสั่งทั้งหมด: make help

.PHONY: help dev dev-stop db-reset db-migrate db-seed db-status types \
        test test-backend test-frontend test-infra \
        build build-backend build-frontend build-aot \
        lint lint-backend lint-frontend \
        setup setup-secrets

DOTNET     := dotnet
NPM        := npm
SUPABASE   := supabase
BACKEND    := backend/Ryla.slnx
FRONTEND   := frontend

# ─────────────────────────────────────────────────────────────────────────────
# Help
# ─────────────────────────────────────────────────────────────────────────────

help: ## แสดงคำสั่งทั้งหมด
	@awk 'BEGIN {FS = ":.*##"; printf "\nUsage:\n  make \033[36m<command>\033[0m\n\nCommands:\n"} \
	/^[a-zA-Z_-]+:.*?##/ { printf "  \033[36m%-20s\033[0m %s\n", $$1, $$2 }' $(MAKEFILE_LIST)

# ─────────────────────────────────────────────────────────────────────────────
# Local Development
# ─────────────────────────────────────────────────────────────────────────────

dev: db-status ## เริ่ม Supabase local (ต้องรัน backend และ frontend แยก)
	@echo ""
	@echo "✅ Supabase is running"
	@echo ""
	@echo "   Start backend:  dotnet run --project backend/src/Ryla.Api"
	@echo "   Start frontend: npm run dev --prefix frontend"
	@echo ""
	@echo "   Studio:         http://localhost:54323"
	@echo "   API:            http://localhost:5000"
	@echo "   Frontend:       http://localhost:3000"

dev-start: ## เริ่ม Supabase local stack
	$(SUPABASE) start

dev-stop: ## หยุด Supabase local stack
	$(SUPABASE) stop

dev-status: ## ดูสถานะ Supabase local
	$(SUPABASE) status

# ─────────────────────────────────────────────────────────────────────────────
# Database
# ─────────────────────────────────────────────────────────────────────────────

db-reset: ## รีเซ็ต database + apply migrations + seed (local dev only)
	$(SUPABASE) db reset
	@echo "✅ Database reset with migrations and seed data"

db-migrate: ## สร้าง migration ใหม่ (ต้องระบุ name=<ชื่อ>)
	@test -n "$(name)" || (echo "❌ ต้องระบุ name: make db-migrate name=add_flows_table" && exit 1)
	$(SUPABASE) migration new $(name)
	@echo "✅ Migration created: supabase/migrations/"

db-diff: ## สร้าง migration จาก schema ปัจจุบัน (ต้องระบุ name=<ชื่อ>)
	@test -n "$(name)" || (echo "❌ ต้องระบุ name: make db-diff name=add_indexes" && exit 1)
	$(SUPABASE) db diff --use-migra -f $(name)

db-seed: ## รัน seed.sql เพิ่มเติม (ไม่ reset schema)
	$(SUPABASE) db execute --file supabase/seed.sql
	@echo "✅ Seed data applied"

db-status: ## ตรวจสอบว่า Supabase local กำลังรันอยู่
	@$(SUPABASE) status 2>/dev/null || (echo "⚠️  Supabase is not running. Run: make dev-start" && exit 1)

# ─────────────────────────────────────────────────────────────────────────────
# Type Generation
# ─────────────────────────────────────────────────────────────────────────────

types: db-status ## Generate TypeScript types จาก Supabase schema
	$(SUPABASE) gen types typescript --local \
	  > $(FRONTEND)/src/types/database.types.ts
	@echo "✅ TypeScript types generated: frontend/src/types/database.types.ts"
	@echo "   ⚠️  ลบ comment header ออกถ้า supabase CLI เพิ่ม 'do not edit' ที่ไม่ตรงกับ format เดิม"

# ─────────────────────────────────────────────────────────────────────────────
# Testing
# ─────────────────────────────────────────────────────────────────────────────

test: test-backend test-frontend ## รัน tests ทั้งหมด

test-backend: ## รัน .NET tests ทั้งหมด (unit + integration)
	$(DOTNET) test $(BACKEND) --configuration Release \
	  --logger "console;verbosity=normal"

test-unit: ## รัน unit tests เท่านั้น (เร็ว, ไม่ต้องการ Docker)
	$(DOTNET) test $(BACKEND) --configuration Release \
	  --filter "Category!=Integration" \
	  --logger "console;verbosity=normal"

test-infra: ## รัน infrastructure integration tests (ต้องการ Docker)
	$(DOTNET) test backend/tests/Ryla.Infrastructure.Tests \
	  --configuration Release \
	  --logger "console;verbosity=normal"

test-frontend: ## รัน Vitest tests
	$(NPM) run test --prefix $(FRONTEND)

test-coverage: ## รัน tests ทั้งหมดพร้อม coverage report
	$(DOTNET) test $(BACKEND) --collect:"XPlat Code Coverage" \
	  --results-directory ./coverage
	$(NPM) run test:coverage --prefix $(FRONTEND)

# ─────────────────────────────────────────────────────────────────────────────
# Build
# ─────────────────────────────────────────────────────────────────────────────

build: build-backend build-frontend ## Build ทั้ง backend และ frontend

build-backend: ## Build .NET backend (Release)
	$(DOTNET) build $(BACKEND) --configuration Release

build-frontend: ## Build Next.js frontend (production)
	$(NPM) run build --prefix $(FRONTEND)

build-aot: ## Build Native AOT binary สำหรับ Linux (Azure Functions)
	$(DOTNET) publish backend/src/Ryla.Api/Ryla.Api.csproj \
	  --configuration Release \
	  --runtime linux-x64 \
	  --self-contained true \
	  /p:PublishAot=true \
	  /warnaserror
	@echo "✅ AOT binary: backend/src/Ryla.Api/bin/Release/net10.0/linux-x64/publish/Ryla.Api"

# ─────────────────────────────────────────────────────────────────────────────
# Linting
# ─────────────────────────────────────────────────────────────────────────────

lint: lint-backend lint-frontend ## รัน linting ทั้งหมด

lint-backend: ## ตรวจสอบ C# formatting
	$(DOTNET) format $(BACKEND) --verify-no-changes --severity error

lint-frontend: ## รัน ESLint + TypeScript type check
	$(NPM) run lint --prefix $(FRONTEND)
	$(NPM) run type-check --prefix $(FRONTEND)

lint-fix: ## แก้ไข C# formatting อัตโนมัติ
	$(DOTNET) format $(BACKEND)

# ─────────────────────────────────────────────────────────────────────────────
# Setup (ครั้งแรก)
# ─────────────────────────────────────────────────────────────────────────────

setup: ## Setup โปรเจคสำหรับ developer ใหม่
	@echo "📦 Installing frontend dependencies..."
	$(NPM) ci --prefix $(FRONTEND)
	@echo "📦 Restoring .NET packages..."
	$(DOTNET) restore $(BACKEND)
	@echo ""
	@echo "✅ Setup complete. ต่อไป:"
	@echo "   1. Copy .env.example ไปเป็น frontend/.env.local แล้วใส่ค่า"
	@echo "   2. make dev-start    (เริ่ม Supabase)"
	@echo "   3. make db-reset     (apply migrations + seed)"
	@echo "   4. make setup-secrets (ตั้งค่า dotnet user-secrets)"
	@echo "   5. make dev          (เริ่ม development)"

setup-secrets: ## ตั้งค่า dotnet user-secrets สำหรับ local development
	@echo "กรอก Supabase local connection string:"
	@echo "  dotnet user-secrets set 'ConnectionStrings:Supabase' 'Host=localhost;Port=54322;Database=postgres;Username=postgres;Password=postgres;'"
	@echo "  --project backend/src/Ryla.Api"
	@echo ""
	@echo "หรือ appsettings.Development.json มีค่า default สำหรับ Supabase local อยู่แล้ว"
