# E2E Smoke Tests

Live HTTP tests ที่ run ต่อ `make test-e2e` เพื่อ validate contract ของ webhook endpoints + full business path

---

## 2 Modes

### Regression mode (default) — ไม่ต้อง DB seed

`make test-e2e` เรียกทุก `tests/e2e/*.py` ตามลำดับ alphabetical

ครอบคลุม:
- HMAC verification contract (200 happy, 401 invalid/missing/expired/tampered, 422 malformed JSON)
- Webhook response codes ไม่เปลี่ยนแม้ use case path skip (SkippedNoTenant)

ไม่ต้องการ:
- Supabase running (app จะ fail-fast แต่ E2E sensor จะไม่เช็ค /health ก่อนยิง)
- Seed data (ทุก webhook จะเข้า SkippedNoTenant path)
- LINE stub

**Prerequisites:**
- App รันที่ `localhost:5282` (ต้อง `make dev` ก่อน — app fail-fast ถ้า DB down)

**Run:**
```bash
make dev                                                # Supabase + DB ready
dotnet run --project backend/src/Ryla.Api ... &         # Backend
make test-e2e
```

**Expected output:**
```
✅ All E2E tests passed   (4 + 9 + 9 = 22 pass, 4 LINE full-mode skipped)
```

---

### Full mode (`RYLA_E2E_LINE=1`) — LINE push end-to-end

เพิ่มเติมจาก regression ครอบคลุม:
- Webhook → DB lookup → LINE API call → stub receives push (200, message format check)
- LINE 401 swallowed (webhook ยังคืน 200)

**Prerequisites (ทำตามลำดับ):**

#### 1. Dev stack + DB ready
```bash
make dev
```

#### 2. Seed E2E test tenant + connections
```bash
docker exec -i supabase_db_ryla psql -U postgres -d postgres \
    < tests/e2e/fixtures/line_notification_seed.sql
```

สร้าง:
- Tenant `11111111-1111-1111-1111-111111111111` ชื่อ "E2E Test Tenant"
- Connection tiktok_shop (shop_id=`7010000000001`)
- Connection shopee (shop_id=`900001`)
- Connection line_oa (dummy token + target user ID)

#### 3. เริ่ม LINE stub server (background)
```bash
python3 tests/e2e/helpers/line_stub_server.py &
```

Stub listens at `localhost:9999` รับ POST `/v2/bot/message/push` + endpoints `__received`, `__reset`, `__fail_mode`

#### 4. เริ่ม backend ด้วย LINE base URL override

Option A — ผ่าน `appsettings.Development.json`:
```json
"Line": { "BaseUrl": "http://localhost:9999" }
```

Option B — ผ่าน env var (ต้องไม่ใช้ `--launch-profile` เพราะ profile ไม่ inherit shell env):
```bash
cd backend/src/Ryla.Api
ASPNETCORE_ENVIRONMENT=Development \
Line__BaseUrl=http://localhost:9999 \
dotnet bin/Release/net10.0/Ryla.Api.dll --urls http://localhost:5282
```

#### 5. Run full-mode E2E
```bash
curl -X POST http://localhost:9999/__reset > /dev/null    # clear stub state
RYLA_E2E_LINE=1 python3 tests/e2e/line_notification.py
```

**Expected output:**
```
  8 passed, 0 failed, 0 skipped
```

**Verify stub received pushes:**
```bash
curl -s http://localhost:9999/__received | python3 -m json.tool
```

---

## Teardown

```bash
# Stop backend (Ctrl+C หรือ kill PID)
lsof -ti:5282 | xargs kill

# Stop LINE stub
lsof -ti:9999 | xargs kill

# Stop Supabase
make dev-stop
```

---

## Files

| Path | Purpose |
|---|---|
| `tests/e2e/webhook_tiktok_shop.py` | RYLA-2 HMAC regression (9 cases) |
| `tests/e2e/webhook_shopee.py` | RYLA-3 HMAC regression (9 cases) |
| `tests/e2e/line_notification.py` | RYLA-4 LINE path (4 regression + 4 full-mode) |
| `tests/e2e/helpers/line_stub_server.py` | LINE Messaging API stub (stdlib HTTP) |
| `tests/e2e/fixtures/line_notification_seed.sql` | SQL seed for full mode |

**หมายเหตุ:** `make test-e2e` glob ใช้ `tests/e2e/*.py` flat — ไฟล์ใน `helpers/` + `fixtures/` ไม่โดนจับ (เจตนา)

---

## Troubleshooting

| อาการ | สาเหตุ | แก้ |
|---|---|---|
| App ไม่รัน → E2E fail all cases | DB ไม่ได้เปิด | `make dev` (fail-fast by design) |
| Full mode: `(200, 0)` — stub รับ 0 push | `LineOptions.BaseUrl` ไม่ override | Check `appsettings.Development.json` Line section, หรือใช้ `dotnet` (not `--launch-profile`) |
| Full mode: push ได้แต่ token ไม่ตรง | Seed SQL ไม่ได้ apply | Re-run Step 2 (seed) + verify: `psql -c "SELECT * FROM connections WHERE tenant_id='11111111-1111-1111-1111-111111111111'"` |
| `Address already in use` on stub | Previous stub process ยังรัน | `lsof -ti:9999 \| xargs kill` |
| Regression cases fail หลัง wiring change | Use case throw ไม่ถูก catch | Endpoint try/catch รอบ `orderUseCase.ExecuteAsync` |
