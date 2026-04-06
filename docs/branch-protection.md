# Branch Protection Setup

ตั้งค่า GitHub Branch Protection หลัง push ขึ้น GitHub ครั้งแรก

---

## ขั้นตอน (GitHub Settings → Branches)

### 1. ปกป้อง `main` branch

```
Repository → Settings → Branches → Add branch ruleset
```

| Setting | Value |
|---|---|
| Ruleset name | `main` |
| Enforcement status | Active |
| Target branches | Include: `main` |

**Branch rules:**
- [x] Restrict deletions
- [x] Require a pull request before merging
  - Required approvals: **1**
  - [x] Dismiss stale pull request approvals
  - [x] Require review from Code Owners
- [x] Require status checks to pass
  - [x] Require branches to be up to date
  - Required checks:
    - `CI — Backend (.NET 10) / Static Analysis`
    - `CI — Backend (.NET 10) / Test & Coverage Gate`
    - `CI — Backend (.NET 10) / Native AOT Build (Linux x64)`
    - `CI — Frontend (Next.js) / Static Analysis`
    - `CI — Frontend (Next.js) / Test & Coverage Gate`
    - `CI — Frontend (Next.js) / Production Build`
    - `Security Scan / Secret Detection (Gitleaks)`
    - `Quality Gate / All Checks Passed`
- [x] Require signed commits
- [x] Block force pushes

---

### 2. ปกป้อง `develop` branch

เหมือน `main` แต่ลด required approvals เป็น 1 (หรือ 0 สำหรับ solo dev)

| Setting | Value |
|---|---|
| Ruleset name | `develop` |
| Target branches | Include: `develop` |
| Required approvals | 1 (หรือ 0 ถ้า solo) |
| Required checks | เหมือน `main` |
| Block force pushes | ✅ |

---

## Quality Gate Summary

| Check | เมื่อไหร่ที่ fail | Workflow |
|---|---|---|
| `dotnet format` | มี formatting violation | `ci-backend.yml / lint` |
| ESLint | มี warning หรือ error | `ci-frontend.yml / lint` |
| TypeScript | type error | `ci-frontend.yml / lint` |
| Backend Coverage | < 80% | `ci-backend.yml / test` |
| Frontend Coverage | < 80% | `ci-frontend.yml / test` |
| Native AOT Build | IL2xxx trim warning | `ci-backend.yml / aot-build` |
| Gitleaks | พบ secret ใน git history | `security-scan.yml` |
| npm audit | High/Critical CVE | `security-scan.yml` |

---

## Environment Secrets ที่ต้องตั้งค่า

`Repository → Settings → Secrets and variables → Actions`

| Secret | ใช้สำหรับ |
|---|---|
| `NEXT_PUBLIC_SUPABASE_URL` | Next.js build (optional — ใช้ fallback ถ้าไม่มี) |
| `NEXT_PUBLIC_SUPABASE_ANON_KEY` | Next.js build (optional) |
| `AZURE_FUNCTIONAPP_PUBLISH_PROFILE` | Deploy to Azure Functions (Phase 2) |
