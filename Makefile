# ─────────────────────────────────────────────────────────────────────────────
# Ryla — Developer Commands
# ─────────────────────────────────────────────────────────────────────────────
# ใช้: make <command>
# ดูคำสั่งทั้งหมด: make help

.PHONY: help dev dev-stop db-reset db-migrate db-seed db-status types \
        test test-backend test-frontend test-infra test-unit test-e2e \
        build build-backend build-frontend build-aot \
        lint lint-backend lint-frontend lint-fix \
        aot-check quality-gate quality-gate-full security-check \
        setup setup-secrets \
        changelog-add changelog-check release-notes release \
        context-update agent-status

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

test-e2e: ## รัน E2E smoke tests ด้วย real HTTP (app ต้องรันอยู่ที่ localhost:5282)
	@echo "🔍 Checking app is running at localhost:5282..."
	@curl -sf http://localhost:5282/health > /dev/null || \
	  (echo "❌ App ไม่ได้รัน — รัน 'dotnet run --project backend/src/Ryla.Api' ก่อน" && exit 1)
	@echo "✅ App is healthy"
	@echo ""
	@echo "🧪 Running E2E scripts from tests/e2e/..."
	@if ls tests/e2e/*.py 1>/dev/null 2>&1; then \
	  failed=0; \
	  for script in tests/e2e/*.py; do \
	    echo "  → $$script"; \
	    python3 "$$script" || failed=1; \
	  done; \
	  if [ $$failed -eq 0 ]; then \
	    echo ""; \
	    echo "✅ All E2E tests passed"; \
	  else \
	    echo ""; \
	    echo "❌ E2E tests failed"; \
	    exit 1; \
	  fi; \
	else \
	  echo "⚠️  ไม่พบ E2E scripts ใน tests/e2e/ — ข้าม"; \
	fi

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
# Quality Gates
# ─────────────────────────────────────────────────────────────────────────────

aot-check: ## ตรวจสอบ AOT trim warnings — publish ไปยัง /tmp (ต้อง pass ก่อน merge)
	$(DOTNET) publish backend/src/Ryla.Api/Ryla.Api.csproj \
	  --configuration Release \
	  --runtime linux-x64 \
	  --self-contained true \
	  /p:PublishAot=true \
	  /warnaserror \
	  -o /tmp/ryla-aot-check
	@echo "✅ AOT check passed — zero trim warnings"
	@rm -rf /tmp/ryla-aot-check

quality-gate: lint test-unit aot-check changelog-check ## รัน quality gate (fast — lint + unit + AOT + changelog)
	@echo ""
	@echo "════════════════════════════════════════"
	@echo "  Quality Gate — PASSED"
	@echo "════════════════════════════════════════"
	@echo "  ✅ Lint (C# + TypeScript)"
	@echo "  ✅ Unit tests"
	@echo "  ✅ AOT check (zero IL warnings)"
	@echo "  ✅ Changelog fragment"
	@echo ""
	@echo "  ⚠️  ยังต้องรัน E2E ก่อน create PR:"
	@echo "     dotnet run --project backend/src/Ryla.Api &"
	@echo "     make test-e2e"
	@echo "════════════════════════════════════════"
	@echo ""

quality-gate-full: quality-gate test-e2e ## รัน quality gate ทั้งหมด รวม E2E (app ต้องรันอยู่)
	@echo ""
	@echo "════════════════════════════════════════"
	@echo "  Full Quality Gate — PASSED"
	@echo "════════════════════════════════════════"
	@echo "  ✅ Lint (C# + TypeScript)"
	@echo "  ✅ Unit tests"
	@echo "  ✅ AOT check (zero IL warnings)"
	@echo "  ✅ Changelog fragment"
	@echo "  ✅ E2E smoke tests"
	@echo "  Ready for PR creation"
	@echo "════════════════════════════════════════"
	@echo ""

security-check: ## ตรวจสอบ NuGet + npm vulnerabilities
	@echo "🔍 Checking .NET NuGet packages for vulnerabilities..."
	$(DOTNET) restore $(BACKEND)
	$(DOTNET) list $(BACKEND) package --vulnerable --include-transitive
	@echo ""
	@echo "🔍 Checking npm packages for vulnerabilities..."
	$(NPM) audit --audit-level=high --prefix $(FRONTEND)
	@echo "✅ Security check complete"

# ─────────────────────────────────────────────────────────────────────────────
# Setup (ครั้งแรก)
# ─────────────────────────────────────────────────────────────────────────────

setup: ## Setup โปรเจคสำหรับ developer ใหม่
	@echo "🔧 Configuring git hooks..."
	git config core.hooksPath .githooks
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
	@echo ""
	@echo "   ℹ️  Git hooks ตั้งแล้ว — push ตรงไป main/develop จะถูกบล็อก"

# ─────────────────────────────────────────────────────────────────────────────
# Workflow Automation
# ─────────────────────────────────────────────────────────────────────────────

changelog-add: ## สร้าง changelog fragment สำหรับ PR (ต้องทำก่อน create PR)
	@echo "📝 Changelog Fragment Creator"
	@echo "─────────────────────────────"
	@read -p "PR Number (หรือกด Enter ใช้ branch name): " pr_num; \
	 if [ -z "$$pr_num" ]; then pr_num=$$(git rev-parse --abbrev-ref HEAD | grep -o 'RYLA-[0-9]*' || echo "0"); fi; \
	 read -p "Type [feat/fix/perf/refactor/security/docs/chore]: " type; \
	 read -p "Scope [api/core/infra/frontend/supabase/ci]: " scope; \
	 read -p "Title (< 80 chars, user-facing): " title; \
	 slug=$$(echo "$$title" | tr '[:upper:]' '[:lower:]' | sed 's/[^a-z0-9]/-/g' | tr -s '-' | sed 's/^-//;s/-$$//'); \
	 outfile="changelog/fragments/$$pr_num-$$slug.yml"; \
	 printf "pr: $$pr_num\ntype: $$type\nscope: \"$$scope\"\ntitle: \"$$title\"\ndescription: |\n  (อธิบาย user-facing impact ที่นี่)\nbreaking: false\nmigration: false\n" > "$$outfile"; \
	 echo ""; \
	 echo "✅ Created: $$outfile"; \
	 echo "   แก้ไข description แล้ว commit ไปพร้อมกับ branch"

changelog-check: ## ตรวจสอบว่ามี changelog fragment สำหรับ PR ปัจจุบัน
	@PR_NUM=$$(git rev-parse --abbrev-ref HEAD | grep -o '[0-9]*' | head -1 || echo ''); \
	 if [ -z "$$PR_NUM" ]; then echo "⚠️  ไม่พบ PR number ใน branch name"; exit 0; fi; \
	 FRAGMENT=$$(ls changelog/fragments/$$PR_NUM-*.yml 2>/dev/null | head -1 || echo ''); \
	 if [ -z "$$FRAGMENT" ]; then \
	   echo "❌ ไม่พบ changelog fragment สำหรับ PR #$$PR_NUM"; \
	   echo "   รัน: make changelog-add"; \
	   exit 1; \
	 else \
	   echo "✅ Found changelog fragment: $$FRAGMENT"; \
	 fi

release-notes: ## แสดง release notes จาก changelog fragments (ยังไม่ archive)
	@python3 scripts/compile-changelog.py

release: ## สร้าง release: tag + GitHub Release + archive fragments
	@if [ "$$(git branch --show-current)" != "main" ]; then \
	  echo "❌ ต้องอยู่ใน main branch เพื่อ release"; exit 1; \
	fi
	@read -p "Version (e.g., v0.2.0): " version; \
	 echo ""; \
	 echo "📋 Release Notes:"; \
	 echo "─────────────────"; \
	 python3 scripts/compile-changelog.py --version "$$version"; \
	 echo "─────────────────"; \
	 read -p "สร้าง release $$version? [y/N] " confirm; \
	 if [ "$$confirm" = "y" ] || [ "$$confirm" = "Y" ]; then \
	   notes=$$(python3 scripts/compile-changelog.py --version "$$version"); \
	   git tag -a "$$version" -m "$$version"; \
	   git push origin "$$version"; \
	   gh release create "$$version" --title "$$version" --notes "$$notes"; \
	   python3 scripts/compile-changelog.py --archive "$$version" > /dev/null; \
	   git add changelog/; \
	   git commit -m "chore: archive changelog fragments for $$version"; \
	   echo ""; \
	   echo "✅ Release $$version created!"; \
	   echo "   https://github.com/Vampire-V/Ryla/releases/tag/$$version"; \
	 else \
	   echo "❌ Cancelled"; \
	 fi

context-update: ## เปิด active-context.md เพื่ออัพเดท sprint state
	@echo "Opening .claude/memory/active-context.md..."
	@$${EDITOR:-vi} .claude/memory/active-context.md

agent-status: ## แสดงสถานะ agent tasks ปัจจุบัน
	@echo "📋 Agent Task Claims:"
	@ls .claude/tasks/ 2>/dev/null | grep -v '.gitkeep' || echo "  (ไม่มี active tasks)"
	@echo ""
	@echo "📁 Pending Changelog Fragments:"
	@ls changelog/fragments/ 2>/dev/null | grep -v '.gitkeep' || echo "  (ไม่มี fragments)"

setup-secrets: ## ตั้งค่า dotnet user-secrets สำหรับ local development
	@echo "กรอก Supabase local connection string:"
	@echo "  dotnet user-secrets set 'ConnectionStrings:Supabase' 'Host=localhost;Port=54322;Database=postgres;Username=postgres;Password=postgres;'"
	@echo "  --project backend/src/Ryla.Api"
	@echo ""
	@echo "หรือ appsettings.Development.json มีค่า default สำหรับ Supabase local อยู่แล้ว"
