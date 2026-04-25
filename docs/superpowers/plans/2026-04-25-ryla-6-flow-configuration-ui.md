# RYLA-6: Flow Configuration UI — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build MVP UI that lets sellers connect LINE OA + Google Sheets and send a test notification within 5 minutes of signup.

**Architecture:** Hybrid — frontend writes credentials directly to Supabase via `@supabase/ssr` (RLS-protected). Test-notification button calls a Next.js API route that proxies to a new .NET endpoint (`POST /api/test-notification`). No new CRUD backend endpoints needed.

**Tech Stack:** Next.js 16 (App Router, `proxy.ts` replacing deprecated `middleware.ts`), `@supabase/ssr` 0.10.0, Tailwind CSS 4, Vitest, .NET 10 AOT, xUnit + NSubstitute.

---

## File Map

### New / Modified (Frontend)
| File | Action |
|------|--------|
| `frontend/src/proxy.ts` | Create — Auth guard (Next.js 16 renamed middleware→proxy) |
| `frontend/src/app/(auth)/login/page.tsx` | Create — Google OAuth sign-in button |
| `frontend/src/app/(auth)/auth/callback/route.ts` | Create — Supabase OAuth callback handler |
| `frontend/src/app/(dashboard)/layout.tsx` | Create — Sidebar + main shell |
| `frontend/src/app/(dashboard)/connections/page.tsx` | Create — Connections list |
| `frontend/src/app/(dashboard)/connections/new/[platform]/page.tsx` | Create — Form router |
| `frontend/src/app/(dashboard)/webhook/page.tsx` | Create — Webhook URL display |
| `frontend/src/app/api/test-notification/route.ts` | Create — Next.js API proxy |
| `frontend/src/components/layout/Sidebar.tsx` | Create — Navigation sidebar |
| `frontend/src/components/connections/ConnectionCard.tsx` | Create — Connection card (client) |
| `frontend/src/components/connections/LineForm.tsx` | Create — LINE OA form (client) |
| `frontend/src/components/connections/SheetsForm.tsx` | Create — Google Sheets form (client, drag & drop) |
| `frontend/src/lib/connections.ts` | Create — Supabase CRUD helpers |
| `frontend/src/lib/connections.test.ts` | Create — Vitest unit tests |
| `frontend/.env.local` | Create — Local env vars |
| `supabase/config.toml` | Modify — Add Google OAuth provider |

### New / Modified (Backend)
| File | Action |
|------|--------|
| `backend/src/Ryla.Core/UseCases/ITestNotificationUseCase.cs` | Create |
| `backend/src/Ryla.Core/UseCases/TestNotificationUseCase.cs` | Create |
| `backend/src/Ryla.Core/UseCases/CoreServiceCollectionExtensions.cs` | Modify — register new use case |
| `backend/src/Ryla.Api/Endpoints/TestNotificationEndpoints.cs` | Create |
| `backend/src/Ryla.Api/Extensions/ServiceCollectionExtensions.cs` | Modify — add JSON types |
| `backend/src/Ryla.Api/Program.cs` | Modify — map new endpoint |
| `backend/src/Ryla.Api/appsettings.Development.json` | Modify — add InternalApiSecret |

### New (Database + E2E)
| File | Action |
|------|--------|
| `supabase/migrations/20260425000000_connections_unique_constraint.sql` | Create |
| `changelog/fragments/9-ryla-6-flow-configuration-ui.yml` | Create |
| `tests/e2e/test_notification_api.py` | Create |

---

## Task 1: DB Migration — Unique Constraint for Upsert

**Files:**
- Create: `supabase/migrations/20260425000000_connections_unique_constraint.sql`

The connections table already has RLS (`connections: tenant isolation` from init migration). We only need a UNIQUE constraint on `(tenant_id, platform)` so Supabase `.upsert({ onConflict: 'tenant_id,platform' })` works correctly (one active LINE OA + one Google Sheets per tenant).

- [ ] **Step 1: Write migration file**

```sql
-- supabase/migrations/20260425000000_connections_unique_constraint.sql
-- UNIQUE constraint บน (tenant_id, platform) เพื่อรองรับ upsert จาก frontend
-- แต่ละ tenant มีได้ 1 connection ต่อ platform
ALTER TABLE connections
  ADD CONSTRAINT connections_tenant_platform_unique
  UNIQUE (tenant_id, platform);
```

- [ ] **Step 2: Apply migration to local Supabase**

```bash
supabase db reset
```
Expected: "Finished supabase db reset."

- [ ] **Step 3: Verify constraint exists**

```bash
supabase db diff --use-migra
```
Expected: empty diff (migration applied cleanly).

- [ ] **Step 4: Commit**

```bash
git add supabase/migrations/20260425000000_connections_unique_constraint.sql
git commit -m "feat(supabase): add unique constraint connections(tenant_id, platform) for upsert support"
```

---

## Task 2: Google OAuth — Supabase + Env Setup

**Files:**
- Modify: `supabase/config.toml`
- Create: `frontend/.env.local`

**⚠️ Manual prerequisite (user does this once):**
1. Go to Google Cloud Console → APIs & Services → Credentials
2. Create OAuth 2.0 Client ID (Web app type)
3. Add Authorized redirect URI: `http://localhost:54321/auth/v1/callback`
4. Note the Client ID and Client Secret

- [ ] **Step 1: Add Google provider to Supabase config**

Edit `supabase/config.toml` — append at end:

```toml
[auth.external.google]
enabled = true
client_id = "env(SUPABASE_AUTH_GOOGLE_CLIENT_ID)"
secret = "env(SUPABASE_AUTH_GOOGLE_CLIENT_SECRET)"
```

- [ ] **Step 2: Set Google OAuth env vars**

Create `supabase/.env.local` (gitignored):
```
SUPABASE_AUTH_GOOGLE_CLIENT_ID=<your-google-client-id>
SUPABASE_AUTH_GOOGLE_CLIENT_SECRET=<your-google-client-secret>
```

Verify `supabase/.env.local` is in `.gitignore` — add if missing:
```bash
grep "supabase/.env.local" .gitignore || echo "supabase/.env.local" >> .gitignore
```

- [ ] **Step 3: Create frontend .env.local**

Create `frontend/.env.local`:
```
NEXT_PUBLIC_SUPABASE_URL=http://localhost:54321
NEXT_PUBLIC_SUPABASE_ANON_KEY=eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJpc3MiOiJzdXBhYmFzZS1kZW1vIiwicm9sZSI6ImFub24iLCJleHAiOjE5ODM4MTI5OTZ9.CRFA0NiK7ACcPhanstMlD/sDyO8IwDkUyvadns96/CY
NEXT_PUBLIC_BACKEND_URL=http://localhost:5282
INTERNAL_API_SECRET=ryla-dev-internal-secret
```

Verify `frontend/.env.local` is in `.gitignore`:
```bash
grep "\.env\.local" .gitignore || echo "frontend/.env.local" >> .gitignore
```

- [ ] **Step 4: Add InternalApiSecret to backend dev settings**

Edit `backend/src/Ryla.Api/appsettings.Development.json` — add `"InternalApiSecret"` after `"AllowedHosts"`:

```json
{
  "Logging": { ... },
  "AllowedHosts": "*",
  "InternalApiSecret": "ryla-dev-internal-secret",
  ...rest unchanged...
}
```

The exact edit (add one line after `"AllowedHosts": "*"`):
```json
  "AllowedHosts": "*",
  "InternalApiSecret": "ryla-dev-internal-secret",
```

- [ ] **Step 5: Restart Supabase**

```bash
make dev
```
Expected: Supabase starts, Google OAuth provider active.

- [ ] **Step 6: Commit**

```bash
git add supabase/config.toml .gitignore backend/src/Ryla.Api/appsettings.Development.json
git commit -m "chore(ci): configure Google OAuth provider + internal API secret for dev"
```

---

## Task 3: Auth Proxy + Login Page + OAuth Callback

**Files:**
- Create: `frontend/src/proxy.ts`
- Create: `frontend/src/app/(auth)/login/page.tsx`
- Create: `frontend/src/app/(auth)/auth/callback/route.ts`

> **Next.js 16 breaking change:** `middleware.ts` is deprecated. Use `proxy.ts` at `src/` level with named export `proxy`.

- [ ] **Step 1: Create proxy.ts**

Create `frontend/src/proxy.ts`:

```typescript
import { createServerClient } from '@supabase/ssr'
import { NextResponse, type NextRequest } from 'next/server'

export async function proxy(request: NextRequest) {
  let supabaseResponse = NextResponse.next({ request })

  const supabase = createServerClient(
    process.env['NEXT_PUBLIC_SUPABASE_URL']!,
    process.env['NEXT_PUBLIC_SUPABASE_ANON_KEY']!,
    {
      cookies: {
        getAll() {
          return request.cookies.getAll()
        },
        setAll(cookiesToSet) {
          cookiesToSet.forEach(({ name, value }) => request.cookies.set(name, value))
          supabaseResponse = NextResponse.next({ request })
          cookiesToSet.forEach(({ name, value, options }) =>
            supabaseResponse.cookies.set(name, value, options),
          )
        },
      },
    },
  )

  // Refresh session — must be called before any redirect
  const {
    data: { user },
  } = await supabase.auth.getUser()

  const { pathname } = request.nextUrl
  const isProtected =
    pathname.startsWith('/connections') || pathname.startsWith('/webhook')

  if (!user && isProtected) {
    const url = request.nextUrl.clone()
    url.pathname = '/login'
    return NextResponse.redirect(url)
  }

  if (user && pathname === '/login') {
    const url = request.nextUrl.clone()
    url.pathname = '/connections'
    return NextResponse.redirect(url)
  }

  return supabaseResponse
}

export const config = {
  matcher: ['/connections/:path*', '/webhook/:path*', '/login'],
}
```

- [ ] **Step 2: Create login page**

Create `frontend/src/app/(auth)/login/page.tsx`:

```tsx
'use client'

import { createClient } from '@/lib/supabase/client'

export default function LoginPage() {
  const handleGoogleLogin = async () => {
    const supabase = createClient()
    await supabase.auth.signInWithOAuth({
      provider: 'google',
      options: {
        redirectTo: `${window.location.origin}/auth/callback`,
      },
    })
  }

  return (
    <main className="flex min-h-screen items-center justify-center bg-slate-50">
      <div className="w-full max-w-sm rounded-xl border border-slate-200 bg-white p-8 shadow-sm">
        <div className="mb-6 text-center">
          <h1 className="text-2xl font-bold text-slate-900">Ryla</h1>
          <p className="mt-1 text-sm text-slate-500">เข้าสู่ระบบเพื่อจัดการ integrations</p>
        </div>
        <button
          onClick={handleGoogleLogin}
          className="flex w-full items-center justify-center gap-3 rounded-lg border border-slate-200 bg-white px-4 py-3 text-sm font-medium text-slate-700 transition-colors hover:bg-slate-50 focus:outline-none focus:ring-2 focus:ring-indigo-500"
        >
          <GoogleIcon />
          Sign in with Google
        </button>
      </div>
    </main>
  )
}

function GoogleIcon() {
  return (
    <svg width="18" height="18" viewBox="0 0 18 18" aria-hidden="true">
      <path
        fill="#4285F4"
        d="M17.64 9.2c0-.637-.057-1.251-.164-1.84H9v3.481h4.844c-.209 1.125-.843 2.078-1.796 2.717v2.258h2.908c1.702-1.567 2.684-3.875 2.684-6.615z"
      />
      <path
        fill="#34A853"
        d="M9 18c2.43 0 4.467-.806 5.956-2.184l-2.908-2.258c-.806.54-1.837.86-3.048.86-2.344 0-4.328-1.584-5.036-3.711H.957v2.332C2.438 15.983 5.482 18 9 18z"
      />
      <path
        fill="#FBBC05"
        d="M3.964 10.707c-.18-.54-.282-1.117-.282-1.707s.102-1.167.282-1.707V4.961H.957C.347 6.183 0 7.55 0 9s.348 2.817.957 4.039l3.007-2.332z"
      />
      <path
        fill="#EA4335"
        d="M9 3.58c1.321 0 2.508.454 3.44 1.345l2.582-2.58C13.463.891 11.426 0 9 0 5.482 0 2.438 2.017.957 4.961L3.964 7.293C4.672 5.166 6.656 3.58 9 3.58z"
      />
    </svg>
  )
}
```

- [ ] **Step 3: Create OAuth callback route**

Create directory: `frontend/src/app/(auth)/auth/callback/`

Create `frontend/src/app/(auth)/auth/callback/route.ts`:

```typescript
import { createServerClient } from '@supabase/ssr'
import { NextResponse } from 'next/server'
import { cookies } from 'next/headers'

export async function GET(request: Request) {
  const { searchParams, origin } = new URL(request.url)
  const code = searchParams.get('code')

  if (code) {
    const cookieStore = await cookies()
    const supabase = createServerClient(
      process.env['NEXT_PUBLIC_SUPABASE_URL']!,
      process.env['NEXT_PUBLIC_SUPABASE_ANON_KEY']!,
      {
        cookies: {
          getAll() {
            return cookieStore.getAll()
          },
          setAll(cookiesToSet) {
            cookiesToSet.forEach(({ name, value, options }) =>
              cookieStore.set(name, value, options),
            )
          },
        },
      },
    )
    await supabase.auth.exchangeCodeForSession(code)
  }

  return NextResponse.redirect(`${origin}/connections`)
}
```

- [ ] **Step 4: Verify TypeScript compiles**

```bash
cd frontend && npx tsc --noEmit
```
Expected: no errors.

- [ ] **Step 5: Commit**

```bash
git add frontend/src/proxy.ts frontend/src/app/\(auth\)/
git commit -m "feat(frontend): add Google OAuth login + Supabase callback handler"
```

---

## Task 4: Dashboard Layout — Sidebar + Shell

**Files:**
- Create: `frontend/src/components/layout/Sidebar.tsx`
- Create: `frontend/src/app/(dashboard)/layout.tsx`

- [ ] **Step 1: Create Sidebar component**

Create `frontend/src/components/layout/Sidebar.tsx`:

```tsx
'use client'

import Link from 'next/link'
import { usePathname, useRouter } from 'next/navigation'
import { createClient } from '@/lib/supabase/client'

const navItems = [
  { href: '/connections', label: 'Connections', icon: '⚙️' },
  { href: '/webhook', label: 'Webhook URL', icon: '⚡' },
]

export function Sidebar({ userName }: { userName: string }) {
  const pathname = usePathname()
  const router = useRouter()

  const handleSignOut = async () => {
    const supabase = createClient()
    await supabase.auth.signOut()
    router.push('/login')
  }

  return (
    <aside className="flex w-44 flex-shrink-0 flex-col bg-slate-900 text-slate-200">
      <div className="border-b border-slate-800 px-4 py-4">
        <span className="text-lg font-bold text-indigo-400">Ryla</span>
      </div>
      <nav className="flex-1 py-2">
        {navItems.map(({ href, label, icon }) => (
          <Link
            key={href}
            href={href}
            className={`mx-2 my-0.5 flex items-center gap-2 rounded-md px-3 py-2 text-sm transition-colors ${
              pathname.startsWith(href)
                ? 'bg-indigo-900 font-semibold text-indigo-200'
                : 'text-slate-400 hover:text-slate-200'
            }`}
          >
            <span>{icon}</span>
            {label}
          </Link>
        ))}
      </nav>
      <div className="border-t border-slate-800 px-4 py-3">
        <div className="flex items-center gap-2">
          <div className="flex h-6 w-6 flex-shrink-0 items-center justify-center rounded-full bg-slate-700 text-xs font-bold text-white">
            {userName.charAt(0).toUpperCase()}
          </div>
          <span className="flex-1 truncate text-xs text-slate-400">{userName}</span>
          <button
            onClick={handleSignOut}
            className="text-xs text-slate-500 hover:text-slate-300"
            title="Sign out"
          >
            ↩
          </button>
        </div>
      </div>
    </aside>
  )
}
```

- [ ] **Step 2: Create dashboard layout**

Create `frontend/src/app/(dashboard)/layout.tsx`:

```tsx
import { redirect } from 'next/navigation'
import { createClient } from '@/lib/supabase/server'
import { Sidebar } from '@/components/layout/Sidebar'

export default async function DashboardLayout({
  children,
}: {
  children: React.ReactNode
}) {
  const supabase = await createClient()
  const {
    data: { user },
  } = await supabase.auth.getUser()

  if (!user) redirect('/login')

  const { data: profile } = await supabase
    .from('profiles')
    .select('full_name')
    .eq('id', user.id)
    .single()

  const userName = profile?.full_name ?? user.email ?? 'User'

  return (
    <div className="flex h-screen overflow-hidden">
      <Sidebar userName={userName} />
      <main className="flex-1 overflow-auto bg-slate-50 p-6">{children}</main>
    </div>
  )
}
```

- [ ] **Step 3: Verify TypeScript compiles**

```bash
cd frontend && npx tsc --noEmit
```
Expected: no errors.

- [ ] **Step 4: Commit**

```bash
git add frontend/src/components/layout/Sidebar.tsx frontend/src/app/\(dashboard\)/layout.tsx
git commit -m "feat(frontend): add dashboard layout with sidebar navigation"
```

---

## Task 5: Connections Data Layer

**Files:**
- Create: `frontend/src/lib/connections.ts`
- Create: `frontend/src/lib/connections.test.ts`

- [ ] **Step 1: Write failing tests first**

Create `frontend/src/lib/connections.test.ts`:

```typescript
import { describe, it, expect, vi, beforeEach } from 'vitest'
import {
  getTenantId,
  upsertLineConnection,
  upsertSheetsConnection,
  deleteConnection,
} from './connections'
import type { LineCredentials, GoogleSheetsCredentials } from './connections'

vi.mock('@/lib/supabase/client', () => ({
  createClient: () => mockSupabase,
}))

const mockSelect = vi.fn()
const mockEq = vi.fn()
const mockSingle = vi.fn()
const mockUpsert = vi.fn()
const mockDelete = vi.fn()
const mockDeleteEq = vi.fn()

const mockSupabase = {
  auth: { getUser: vi.fn() },
  from: vi.fn(),
}

beforeEach(() => {
  vi.clearAllMocks()
  mockSelect.mockReturnThis()
  mockEq.mockReturnThis()
})

describe('getTenantId', () => {
  it('returns tenant_id when user is authenticated', async () => {
    mockSupabase.auth.getUser.mockResolvedValue({
      data: { user: { id: 'user-abc' } },
      error: null,
    })
    mockSingle.mockResolvedValue({ data: { tenant_id: 'tenant-xyz' }, error: null })
    mockSupabase.from.mockReturnValue({
      select: mockSelect,
      eq: mockEq,
      single: mockSingle,
    })
    mockSelect.mockReturnValue({ eq: mockEq })
    mockEq.mockReturnValue({ single: mockSingle })

    const result = await getTenantId()

    expect(result).toBe('tenant-xyz')
    expect(mockSupabase.from).toHaveBeenCalledWith('profiles')
  })

  it('throws when user is null', async () => {
    mockSupabase.auth.getUser.mockResolvedValue({ data: { user: null }, error: null })

    await expect(getTenantId()).rejects.toThrow('Not authenticated')
  })

  it('throws when profile not found', async () => {
    mockSupabase.auth.getUser.mockResolvedValue({
      data: { user: { id: 'user-abc' } },
      error: null,
    })
    mockSingle.mockResolvedValue({ data: null, error: { message: 'not found' } })
    mockSupabase.from.mockReturnValue({
      select: vi.fn().mockReturnValue({ eq: vi.fn().mockReturnValue({ single: mockSingle }) }),
    })

    await expect(getTenantId()).rejects.toThrow('Profile not found')
  })
})

describe('upsertLineConnection', () => {
  it('calls upsert with line_oa platform and correct credentials', async () => {
    mockUpsert.mockResolvedValue({ error: null })
    mockSupabase.from.mockReturnValue({ upsert: mockUpsert })

    const creds: LineCredentials = {
      channel_access_token: 'tok-123',
      target_user_id: 'Uabc',
    }
    await upsertLineConnection('tenant-1', creds)

    expect(mockUpsert).toHaveBeenCalledWith(
      expect.objectContaining({
        tenant_id: 'tenant-1',
        platform: 'line_oa',
        credentials: creds,
        is_active: true,
      }),
      { onConflict: 'tenant_id,platform' },
    )
  })

  it('throws on Supabase error', async () => {
    mockSupabase.from.mockReturnValue({
      upsert: vi.fn().mockResolvedValue({ error: { message: 'unique violation' } }),
    })

    await expect(
      upsertLineConnection('t1', { channel_access_token: 'x', target_user_id: 'y' }),
    ).rejects.toMatchObject({ message: 'unique violation' })
  })
})

describe('upsertSheetsConnection', () => {
  it('calls upsert with google_sheets platform', async () => {
    mockUpsert.mockResolvedValue({ error: null })
    mockSupabase.from.mockReturnValue({ upsert: mockUpsert })

    const creds: GoogleSheetsCredentials = {
      spreadsheet_id: 'sheet-id',
      worksheet_name: 'Orders',
      service_account_json: '{"type":"service_account"}',
    }
    await upsertSheetsConnection('tenant-1', creds)

    expect(mockUpsert).toHaveBeenCalledWith(
      expect.objectContaining({ platform: 'google_sheets', credentials: creds }),
      { onConflict: 'tenant_id,platform' },
    )
  })
})

describe('deleteConnection', () => {
  it('calls delete().eq() with connection id', async () => {
    mockDeleteEq.mockResolvedValue({ error: null })
    mockDelete.mockReturnValue({ eq: mockDeleteEq })
    mockSupabase.from.mockReturnValue({ delete: mockDelete })

    await deleteConnection('conn-999')

    expect(mockDeleteEq).toHaveBeenCalledWith('id', 'conn-999')
  })
})
```

- [ ] **Step 2: Run tests to confirm they fail**

```bash
cd frontend && npx vitest run src/lib/connections.test.ts
```
Expected: FAIL — `Cannot find module './connections'`

- [ ] **Step 3: Implement connections.ts**

Create `frontend/src/lib/connections.ts`:

```typescript
import { createClient } from '@/lib/supabase/client'
import type { Tables } from '@/types/database.types'

export type Connection = Tables<'connections'>

export type LineCredentials = {
  channel_access_token: string
  target_user_id: string
}

export type GoogleSheetsCredentials = {
  spreadsheet_id: string
  worksheet_name: string
  service_account_json: string
}

export async function getTenantId(): Promise<string> {
  const supabase = createClient()
  const {
    data: { user },
    error: userError,
  } = await supabase.auth.getUser()
  if (userError || !user) throw new Error('Not authenticated')

  const { data, error } = await supabase
    .from('profiles')
    .select('tenant_id')
    .eq('id', user.id)
    .single()

  if (error || !data) throw new Error('Profile not found')
  return data.tenant_id
}

export async function getConnections(): Promise<Connection[]> {
  const supabase = createClient()
  const { data, error } = await supabase
    .from('connections')
    .select('*')
    .order('created_at', { ascending: true })

  if (error) throw error
  return data ?? []
}

export async function upsertLineConnection(
  tenantId: string,
  credentials: LineCredentials,
): Promise<void> {
  const supabase = createClient()
  const { error } = await supabase
    .from('connections')
    .upsert(
      {
        tenant_id: tenantId,
        platform: 'line_oa',
        credentials: credentials as unknown as Record<string, string>,
        is_active: true,
      },
      { onConflict: 'tenant_id,platform' },
    )
  if (error) throw error
}

export async function upsertSheetsConnection(
  tenantId: string,
  credentials: GoogleSheetsCredentials,
): Promise<void> {
  const supabase = createClient()
  const { error } = await supabase
    .from('connections')
    .upsert(
      {
        tenant_id: tenantId,
        platform: 'google_sheets',
        credentials: credentials as unknown as Record<string, string>,
        is_active: true,
      },
      { onConflict: 'tenant_id,platform' },
    )
  if (error) throw error
}

export async function deleteConnection(id: string): Promise<void> {
  const supabase = createClient()
  const { error } = await supabase.from('connections').delete().eq('id', id)
  if (error) throw error
}
```

- [ ] **Step 4: Run tests and confirm they pass**

```bash
cd frontend && npx vitest run src/lib/connections.test.ts
```
Expected: all tests PASS.

- [ ] **Step 5: Commit**

```bash
git add frontend/src/lib/connections.ts frontend/src/lib/connections.test.ts
git commit -m "feat(frontend): add connections data layer with Supabase CRUD helpers"
```

---

## Task 6: Connections List Page + ConnectionCard

**Files:**
- Create: `frontend/src/components/connections/ConnectionCard.tsx`
- Create: `frontend/src/app/(dashboard)/connections/page.tsx`

- [ ] **Step 1: Create ConnectionCard component**

Create `frontend/src/components/connections/ConnectionCard.tsx`:

```tsx
'use client'

import { useState } from 'react'
import { useRouter } from 'next/navigation'
import { deleteConnection } from '@/lib/connections'
import type { Connection } from '@/lib/connections'

type Props = { connection: Connection }

export function ConnectionCard({ connection }: Props) {
  const router = useRouter()
  const [testLoading, setTestLoading] = useState(false)
  const [toast, setToast] = useState<{ message: string; ok: boolean } | null>(null)

  const creds = connection.credentials as Record<string, string>
  const isLine = connection.platform === 'line_oa'
  const isSheets = connection.platform === 'google_sheets'
  const editHref = `/connections/new/${isLine ? 'line' : 'google-sheets'}`

  const showToast = (message: string, ok: boolean) => {
    setToast({ message, ok })
    setTimeout(() => setToast(null), 3500)
  }

  const handleDelete = async () => {
    if (!confirm('ลบ connection นี้? ไม่สามารถย้อนกลับได้')) return
    await deleteConnection(connection.id)
    router.refresh()
  }

  const handleTest = async () => {
    setTestLoading(true)
    try {
      const res = await fetch('/api/test-notification', { method: 'POST' })
      const json = (await res.json()) as { success: boolean; message: string }
      showToast(
        json.success ? `✅ ${json.message}` : `❌ ${json.message}`,
        json.success,
      )
    } catch {
      showToast('❌ เกิดข้อผิดพลาด กรุณาลองใหม่', false)
    } finally {
      setTestLoading(false)
    }
  }

  return (
    <div className="relative mb-3 rounded-xl border border-slate-200 bg-white p-4 shadow-sm">
      {toast && (
        <div
          className={`absolute right-3 top-3 z-10 rounded-lg px-3 py-2 text-sm font-medium ${
            toast.ok ? 'bg-green-50 text-green-700' : 'bg-red-50 text-red-700'
          }`}
        >
          {toast.message}
        </div>
      )}

      <div className="flex items-center gap-3">
        <div
          className={`flex h-9 w-9 flex-shrink-0 items-center justify-center rounded-lg text-lg text-white ${
            isLine ? 'bg-[#00B900]' : 'bg-blue-600'
          }`}
        >
          {isLine ? '💬' : '📊'}
        </div>
        <div className="min-w-0 flex-1">
          <div className="font-semibold text-slate-900">{isLine ? 'LINE OA' : 'Google Sheets'}</div>
          <div className="truncate text-xs text-slate-500">
            {isLine
              ? `target: ${creds['target_user_id']?.slice(0, 18)}...`
              : `${creds['spreadsheet_id']?.slice(0, 18)}... · tab: ${creds['worksheet_name'] ?? 'Orders'}`}
          </div>
        </div>
        <span className="flex-shrink-0 rounded-full bg-green-50 px-2 py-0.5 text-xs font-semibold text-green-700">
          ● Connected
        </span>
      </div>

      <div className="mt-3 flex flex-wrap gap-2 border-t border-slate-100 pt-3">
        {isLine && (
          <button
            onClick={handleTest}
            disabled={testLoading}
            className="rounded-md border border-green-200 bg-green-50 px-3 py-1 text-xs text-green-700 transition-colors hover:bg-green-100 disabled:opacity-50"
          >
            {testLoading ? '...' : '⚡ ส่งทดสอบ'}
          </button>
        )}
        {isSheets && creds['spreadsheet_id'] && (
          <a
            href={`https://docs.google.com/spreadsheets/d/${creds['spreadsheet_id']}/edit`}
            target="_blank"
            rel="noreferrer"
            className="rounded-md border border-blue-200 bg-blue-50 px-3 py-1 text-xs text-blue-700 hover:bg-blue-100"
          >
            🔗 เปิด Spreadsheet
          </a>
        )}
        <a
          href={editHref}
          className="rounded-md border border-slate-200 bg-white px-3 py-1 text-xs text-slate-600 hover:bg-slate-50"
        >
          ✏️ แก้ไข
        </a>
        <button
          onClick={handleDelete}
          className="ml-auto rounded-md border border-red-100 bg-white px-3 py-1 text-xs text-red-500 hover:bg-red-50"
        >
          🗑 ลบ
        </button>
      </div>
    </div>
  )
}
```

- [ ] **Step 2: Create connections list page**

Create `frontend/src/app/(dashboard)/connections/page.tsx`:

```tsx
import Link from 'next/link'
import { createClient } from '@/lib/supabase/server'
import { ConnectionCard } from '@/components/connections/ConnectionCard'

export default async function ConnectionsPage() {
  const supabase = await createClient()
  const { data: connections } = await supabase
    .from('connections')
    .select('*')
    .in('platform', ['line_oa', 'google_sheets'])
    .order('created_at')

  const lineConn = connections?.find((c) => c.platform === 'line_oa')
  const sheetsConn = connections?.find((c) => c.platform === 'google_sheets')

  return (
    <div>
      <div className="mb-6 flex items-center justify-between">
        <div>
          <h1 className="text-lg font-semibold text-slate-900">Connections</h1>
          <p className="mt-0.5 text-sm text-slate-500">เชื่อมต่อ platform เพื่อรับการแจ้งเตือน</p>
        </div>
        <div className="flex gap-2">
          {!lineConn && (
            <Link
              href="/connections/new/line"
              className="rounded-md bg-indigo-600 px-3 py-2 text-sm font-medium text-white hover:bg-indigo-700"
            >
              + เพิ่ม LINE OA
            </Link>
          )}
          {!sheetsConn && (
            <Link
              href="/connections/new/google-sheets"
              className="rounded-md bg-indigo-600 px-3 py-2 text-sm font-medium text-white hover:bg-indigo-700"
            >
              + เพิ่ม Google Sheets
            </Link>
          )}
        </div>
      </div>

      {lineConn && <ConnectionCard connection={lineConn} />}
      {sheetsConn && <ConnectionCard connection={sheetsConn} />}

      {/* Shopee — deferred */}
      <div className="mb-3 rounded-xl border border-dashed border-slate-300 bg-white p-4 opacity-60">
        <div className="flex items-center gap-3">
          <div className="flex h-9 w-9 flex-shrink-0 items-center justify-center rounded-lg bg-orange-500 text-lg text-white">
            🛒
          </div>
          <div className="flex-1">
            <div className="font-semibold text-slate-900">Shopee</div>
            <div className="text-xs text-slate-400">ต้องมี Partner Key จาก Shopee Open Platform</div>
          </div>
          <span className="rounded-full bg-slate-100 px-2 py-0.5 text-xs text-slate-400">
            ยังไม่เชื่อมต่อ
          </span>
        </div>
      </div>
    </div>
  )
}
```

- [ ] **Step 3: Verify TypeScript**

```bash
cd frontend && npx tsc --noEmit
```
Expected: no errors.

- [ ] **Step 4: Commit**

```bash
git add frontend/src/components/connections/ConnectionCard.tsx \
        frontend/src/app/\(dashboard\)/connections/page.tsx
git commit -m "feat(frontend): add connections list page with ConnectionCard component"
```

---

## Task 7: LINE Connection Form

**Files:**
- Create: `frontend/src/components/connections/LineForm.tsx`

> **Note:** `TopBar` is intentionally omitted — the Sidebar handles all page chrome (branding, user avatar, sign out). `[platform]/page.tsx` with server-side edit pre-fill is created in Task 8 once `SheetsForm` exists.

- [ ] **Step 1: Create LineForm component**

Create `frontend/src/components/connections/LineForm.tsx`:

```tsx
'use client'

import { useState } from 'react'
import { useRouter } from 'next/navigation'
import { getTenantId, upsertLineConnection } from '@/lib/connections'

type Props = {
  initialToken?: string
  initialUserId?: string
}

export function LineForm({ initialToken = '', initialUserId = '' }: Props) {
  const router = useRouter()
  const [token, setToken] = useState(initialToken)
  const [userId, setUserId] = useState(initialUserId)
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState('')

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault()
    setError('')

    if (!token.trim() || !userId.trim()) {
      setError('กรุณากรอกข้อมูลให้ครบทุกช่อง')
      return
    }

    setLoading(true)
    try {
      const tenantId = await getTenantId()
      await upsertLineConnection(tenantId, {
        channel_access_token: token.trim(),
        target_user_id: userId.trim(),
      })
      router.push('/connections')
      router.refresh()
    } catch (err) {
      setError(err instanceof Error ? err.message : 'เกิดข้อผิดพลาด กรุณาลองใหม่')
    } finally {
      setLoading(false)
    }
  }

  return (
    <form onSubmit={handleSubmit} className="max-w-md">
      <div className="mb-5 flex items-center gap-3">
        <div className="flex h-9 w-9 items-center justify-center rounded-lg bg-[#00B900] text-xl text-white">
          💬
        </div>
        <div>
          <h2 className="font-bold text-slate-900">เชื่อมต่อ LINE OA</h2>
          <p className="text-xs text-slate-500">ใส่ credentials จาก LINE Developers Console</p>
        </div>
      </div>

      <div className="mb-4">
        <label className="mb-1 block text-sm font-semibold text-slate-700">
          Channel Access Token <span className="text-red-500">*</span>
        </label>
        <input
          type="text"
          value={token}
          onChange={(e) => setToken(e.target.value)}
          placeholder="pivLt6NPJ0oL7kAe..."
          className="w-full rounded-md border border-slate-300 px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-indigo-500"
        />
        <p className="mt-1 text-xs text-slate-400">
          LINE Developers Console → Messaging API tab → Channel access token (long-lived)
        </p>
      </div>

      <div className="mb-4">
        <label className="mb-1 block text-sm font-semibold text-slate-700">
          Target User ID <span className="text-red-500">*</span>
        </label>
        <input
          type="text"
          value={userId}
          onChange={(e) => setUserId(e.target.value)}
          placeholder="U085b665a102afe19..."
          className="w-full rounded-md border border-slate-300 px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-indigo-500"
        />
        <p className="mt-1 text-xs text-slate-400">
          LINE OA Manager → Basic settings → Your user ID
        </p>
      </div>

      <div className="mb-4 rounded-md border border-green-200 bg-green-50 p-3 text-xs text-green-700">
        💡 ทดสอบ LINE จะส่งข้อความไปยัง User ID ที่กรอก
      </div>

      {error && (
        <div className="mb-3 rounded-md bg-red-50 p-2 text-xs text-red-600">{error}</div>
      )}

      <div className="flex gap-2">
        <button
          type="submit"
          disabled={loading}
          className="flex-1 rounded-md bg-indigo-600 py-2 text-sm font-medium text-white hover:bg-indigo-700 disabled:opacity-50"
        >
          {loading ? 'กำลังบันทึก...' : 'บันทึก'}
        </button>
        <button
          type="button"
          onClick={() => router.push('/connections')}
          className="rounded-md border border-slate-200 px-4 py-2 text-sm text-slate-600 hover:bg-slate-50"
        >
          ยกเลิก
        </button>
      </div>
    </form>
  )
}
```

- [ ] **Step 2: Verify TypeScript**

```bash
cd frontend && npx tsc --noEmit
```
Expected: no errors.

- [ ] **Step 3: Commit**

```bash
git add frontend/src/components/connections/LineForm.tsx
git commit -m "feat(frontend): add LINE OA connection form"
```

---

## Task 8: Google Sheets Connection Form (Drag & Drop)

**Files:**
- Create: `frontend/src/components/connections/SheetsForm.tsx`
- Create: `frontend/src/app/(dashboard)/connections/new/[platform]/page.tsx` (with server-side edit pre-fill)

- [ ] **Step 1: Create SheetsForm component**

Create `frontend/src/components/connections/SheetsForm.tsx`:

```tsx
'use client'

import { useRef, useState } from 'react'
import { useRouter } from 'next/navigation'
import { getTenantId, upsertSheetsConnection } from '@/lib/connections'

type ServiceAccountKey = {
  type: string
  client_email: string
  private_key: string
  token_uri: string
}

type Props = {
  initialSpreadsheetId?: string
  initialWorksheetName?: string
}

export function SheetsForm({ initialSpreadsheetId = '', initialWorksheetName = 'Orders' }: Props) {
  const router = useRouter()
  const fileInputRef = useRef<HTMLInputElement>(null)
  const [spreadsheetId, setSpreadsheetId] = useState(initialSpreadsheetId)
  const [worksheetName, setWorksheetName] = useState(initialWorksheetName)
  const [parsedEmail, setParsedEmail] = useState('')
  const [serviceAccountJson, setServiceAccountJson] = useState('')
  const [dragging, setDragging] = useState(false)
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState('')

  const parseFile = (text: string) => {
    try {
      const parsed = JSON.parse(text) as ServiceAccountKey
      if (!parsed.client_email || !parsed.private_key) {
        setError('ไฟล์ JSON ต้องมี client_email และ private_key')
        return
      }
      const filtered: ServiceAccountKey = {
        type: parsed.type,
        client_email: parsed.client_email,
        private_key: parsed.private_key,
        token_uri: parsed.token_uri,
      }
      setServiceAccountJson(JSON.stringify(filtered))
      setParsedEmail(parsed.client_email)
      setError('')
    } catch {
      setError('ไฟล์ JSON ไม่ถูกต้อง กรุณาใช้ไฟล์ที่ดาวน์โหลดจาก Google Cloud')
    }
  }

  const handleDrop = (e: React.DragEvent) => {
    e.preventDefault()
    setDragging(false)
    const file = e.dataTransfer.files[0]
    if (!file) return
    const reader = new FileReader()
    reader.onload = (ev) => parseFile(ev.target?.result as string)
    reader.readAsText(file)
  }

  const handleFileChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0]
    if (!file) return
    const reader = new FileReader()
    reader.onload = (ev) => parseFile(ev.target?.result as string)
    reader.readAsText(file)
  }

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault()
    setError('')

    if (!serviceAccountJson) {
      setError('กรุณาอัปโหลดไฟล์ Service Account JSON')
      return
    }
    if (!spreadsheetId.trim()) {
      setError('กรุณาใส่ Spreadsheet ID')
      return
    }

    setLoading(true)
    try {
      const tenantId = await getTenantId()
      await upsertSheetsConnection(tenantId, {
        spreadsheet_id: spreadsheetId.trim(),
        worksheet_name: worksheetName.trim() || 'Orders',
        service_account_json: serviceAccountJson,
      })
      router.push('/connections')
      router.refresh()
    } catch (err) {
      setError(err instanceof Error ? err.message : 'เกิดข้อผิดพลาด กรุณาลองใหม่')
    } finally {
      setLoading(false)
    }
  }

  return (
    <form onSubmit={handleSubmit} className="max-w-md">
      <div className="mb-5 flex items-center gap-3">
        <div className="flex h-9 w-9 items-center justify-center rounded-lg bg-blue-600 text-xl text-white">
          📊
        </div>
        <div>
          <h2 className="font-bold text-slate-900">เชื่อมต่อ Google Sheets</h2>
          <p className="text-xs text-slate-500">ต้องการ Service Account JSON + Spreadsheet ID</p>
        </div>
      </div>

      {/* Drag & Drop zone */}
      <div className="mb-4">
        <label className="mb-1 block text-sm font-semibold text-slate-700">
          Service Account Key (JSON) <span className="text-red-500">*</span>
        </label>
        <div
          onClick={() => fileInputRef.current?.click()}
          onDrop={handleDrop}
          onDragOver={(e) => { e.preventDefault(); setDragging(true) }}
          onDragLeave={() => setDragging(false)}
          className={`flex cursor-pointer flex-col items-center justify-center rounded-lg border-2 p-6 text-center transition-colors ${
            dragging
              ? 'border-indigo-400 bg-indigo-50'
              : parsedEmail
              ? 'border-green-300 bg-green-50'
              : 'border-dashed border-indigo-300 bg-violet-50 hover:bg-violet-100'
          }`}
        >
          {parsedEmail ? (
            <>
              <span className="text-2xl">✅</span>
              <p className="mt-1 text-sm font-semibold text-green-700">โหลดสำเร็จ</p>
              <p className="mt-0.5 text-xs text-slate-500 break-all">{parsedEmail}</p>
            </>
          ) : (
            <>
              <span className="text-3xl">📂</span>
              <p className="mt-1 text-sm font-semibold text-indigo-700">ลากไฟล์ .json มาวาง</p>
              <p className="text-xs text-violet-600">หรือคลิกเพื่อเลือกไฟล์</p>
            </>
          )}
        </div>
        <input
          ref={fileInputRef}
          type="file"
          accept=".json,application/json"
          onChange={handleFileChange}
          className="hidden"
        />
        <p className="mt-1 text-xs text-slate-400">
          ดาวน์โหลดจาก Google Cloud → IAM & Admin → Service Accounts → Keys
        </p>
      </div>

      <div className="mb-4">
        <label className="mb-1 block text-sm font-semibold text-slate-700">
          Spreadsheet ID <span className="text-red-500">*</span>
        </label>
        <input
          type="text"
          value={spreadsheetId}
          onChange={(e) => setSpreadsheetId(e.target.value)}
          placeholder="1Spg-ht3g6D5V9j2xNTP..."
          className="w-full rounded-md border border-slate-300 px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-indigo-500"
        />
        <p className="mt-1 text-xs text-slate-400">
          จาก URL: docs.google.com/spreadsheets/d/<strong>[ID นี้]</strong>/edit
        </p>
      </div>

      <div className="mb-4">
        <label className="mb-1 block text-sm font-semibold text-slate-700">ชื่อ Sheet Tab</label>
        <input
          type="text"
          value={worksheetName}
          onChange={(e) => setWorksheetName(e.target.value)}
          placeholder="Orders"
          className="w-full rounded-md border border-slate-300 px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-indigo-500"
        />
        <p className="mt-1 text-xs text-slate-400">ค่า default: Orders</p>
      </div>

      {error && (
        <div className="mb-3 rounded-md bg-red-50 p-2 text-xs text-red-600">{error}</div>
      )}

      <div className="flex gap-2">
        <button
          type="submit"
          disabled={loading}
          className="flex-1 rounded-md bg-indigo-600 py-2 text-sm font-medium text-white hover:bg-indigo-700 disabled:opacity-50"
        >
          {loading ? 'กำลังบันทึก...' : 'บันทึก'}
        </button>
        <button
          type="button"
          onClick={() => router.push('/connections')}
          className="rounded-md border border-slate-200 px-4 py-2 text-sm text-slate-600 hover:bg-slate-50"
        >
          ยกเลิก
        </button>
      </div>
    </form>
  )
}
```

- [ ] **Step 2: Create platform page with server-side edit pre-fill**

Create `frontend/src/app/(dashboard)/connections/new/[platform]/page.tsx`:

```tsx
import { notFound } from 'next/navigation'
import { createClient } from '@/lib/supabase/server'
import { LineForm } from '@/components/connections/LineForm'
import { SheetsForm } from '@/components/connections/SheetsForm'
import type { LineCredentials, GoogleSheetsCredentials } from '@/lib/connections'

type Props = {
  params: Promise<{ platform: string }>
}

export default async function NewConnectionPage({ params }: Props) {
  const { platform } = await params
  const supabase = await createClient()

  if (platform === 'line') {
    const { data: conn } = await supabase
      .from('connections')
      .select('credentials')
      .eq('platform', 'line_oa')
      .maybeSingle()

    const creds = conn?.credentials as LineCredentials | null
    return (
      <div>
        <h1 className="mb-6 text-lg font-semibold text-slate-900">
          {creds ? 'แก้ไข LINE OA' : 'เพิ่ม LINE OA'}
        </h1>
        <LineForm
          initialToken={creds?.channel_access_token ?? ''}
          initialUserId={creds?.target_user_id ?? ''}
        />
      </div>
    )
  }

  if (platform === 'google-sheets') {
    const { data: conn } = await supabase
      .from('connections')
      .select('credentials')
      .eq('platform', 'google_sheets')
      .maybeSingle()

    const creds = conn?.credentials as GoogleSheetsCredentials | null
    return (
      <div>
        <h1 className="mb-6 text-lg font-semibold text-slate-900">
          {creds ? 'แก้ไข Google Sheets' : 'เพิ่ม Google Sheets'}
        </h1>
        <SheetsForm
          initialSpreadsheetId={creds?.spreadsheet_id ?? ''}
          initialWorksheetName={creds?.worksheet_name ?? 'Orders'}
        />
      </div>
    )
  }

  notFound()
}
```

> Service account JSON is intentionally not pre-filled — it would expose the private key in the page source. The user must re-upload the JSON file when editing.

- [ ] **Step 3: Verify TypeScript**

```bash
cd frontend && npx tsc --noEmit
```
Expected: no errors.

- [ ] **Step 4: Commit**

```bash
git add frontend/src/components/connections/SheetsForm.tsx \
        frontend/src/app/\(dashboard\)/connections/new/\[platform\]/page.tsx
git commit -m "feat(frontend): add Google Sheets connection form with drag & drop JSON upload"
```

---

## Task 9: Webhook URL Page

**Files:**
- Create: `frontend/src/app/(dashboard)/webhook/page.tsx`

- [ ] **Step 1: Create webhook page**

Create `frontend/src/app/(dashboard)/webhook/page.tsx`:

```tsx
'use client'

import { useState } from 'react'

const WEBHOOK_URL = 'https://your-domain.com/webhooks/shopee'

export default function WebhookPage() {
  const [copied, setCopied] = useState(false)

  const handleCopy = async () => {
    await navigator.clipboard.writeText(WEBHOOK_URL)
    setCopied(true)
    setTimeout(() => setCopied(false), 2000)
  }

  return (
    <div>
      <h1 className="mb-1 text-lg font-semibold text-slate-900">Webhook URL</h1>
      <p className="mb-6 text-sm text-slate-500">
        นำ URL นี้ไปตั้งค่าใน Shopee Open Platform → Webhook Settings
      </p>

      <div className="max-w-xl rounded-xl border border-slate-200 bg-white p-5 shadow-sm">
        <div className="mb-3 text-xs font-semibold uppercase tracking-wide text-slate-400">
          Shopee Webhook URL
        </div>
        <div className="flex items-center gap-2">
          <code className="flex-1 rounded-md bg-slate-50 px-3 py-2 text-sm text-slate-800 break-all">
            {WEBHOOK_URL}
          </code>
          <button
            onClick={handleCopy}
            className="flex-shrink-0 rounded-md border border-slate-200 px-3 py-2 text-sm text-slate-600 hover:bg-slate-50 transition-colors"
          >
            {copied ? '✅ Copied' : '📋 Copy'}
          </button>
        </div>

        <div className="mt-4 rounded-md bg-amber-50 p-3 text-xs text-amber-800">
          <p className="font-semibold">วิธีตั้งค่าใน Shopee Open Platform:</p>
          <ol className="mt-1 ml-4 list-decimal space-y-1">
            <li>เข้า Shopee Open Platform → My Apps → [app ของคุณ]</li>
            <li>ไปที่ Webhook Settings</li>
            <li>วาง URL ด้านบนใน Order Status Update URL</li>
            <li>กด Save และรอ Shopee ยืนยัน</li>
          </ol>
        </div>
      </div>
    </div>
  )
}
```

- [ ] **Step 2: Verify TypeScript**

```bash
cd frontend && npx tsc --noEmit
```
Expected: no errors.

- [ ] **Step 3: Commit**

```bash
git add frontend/src/app/\(dashboard\)/webhook/page.tsx
git commit -m "feat(frontend): add webhook URL display page with copy-to-clipboard"
```

---

## Task 10: Backend — Test Notification Use Case

**Files:**
- Create: `backend/src/Ryla.Core/UseCases/ITestNotificationUseCase.cs`
- Create: `backend/src/Ryla.Core/UseCases/TestNotificationUseCase.cs`
- Modify: `backend/src/Ryla.Core/UseCases/CoreServiceCollectionExtensions.cs`

- [ ] **Step 1: Write failing unit test first**

Create `backend/tests/Ryla.Core.Tests/UseCases/TestNotificationUseCaseTests.cs`:

```csharp
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Ryla.Core.Domain.Integrations;
using Ryla.Core.Ports.Outbound;
using Ryla.Core.UseCases;
using Xunit;

namespace Ryla.Core.Tests.UseCases;

public class TestNotificationUseCaseTests
{
    private readonly IConnectionRepository _connections = Substitute.For<IConnectionRepository>();
    private readonly ILineNotifier _notifier = Substitute.For<ILineNotifier>();
    private readonly TestNotificationUseCase _sut;

    public TestNotificationUseCaseTests()
    {
        _sut = new TestNotificationUseCase(
            _connections,
            _notifier,
            NullLogger<TestNotificationUseCase>.Instance);
    }

    [Fact]
    public async Task ExecuteAsync__WhenLineCredentialsExist__ShouldSendPushAndReturnSuccess()
    {
        var tenantId = Guid.NewGuid();
        var creds = new LineCredentials("tok-abc", "Utest");

        _connections.GetLineCredentialsAsync(tenantId).Returns(creds);
        _notifier.PushAsync("tok-abc", "Utest", Arg.Any<string>()).Returns(new LineNotifyResult(true));

        var result = await _sut.ExecuteAsync(tenantId);

        Assert.True(result.Success);
        Assert.Null(result.ErrorMessage);
        await _notifier.Received(1).PushAsync("tok-abc", "Utest", Arg.Any<string>());
    }

    [Fact]
    public async Task ExecuteAsync__WhenNoLineConnection__ShouldReturnFailureWithMessage()
    {
        _connections.GetLineCredentialsAsync(Arg.Any<Guid>()).Returns((LineCredentials?)null);

        var result = await _sut.ExecuteAsync(Guid.NewGuid());

        Assert.False(result.Success);
        Assert.NotNull(result.ErrorMessage);
        await _notifier.DidNotReceive().PushAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>());
    }

    [Fact]
    public async Task ExecuteAsync__WhenLinePushFails__ShouldReturnFailureWithError()
    {
        var tenantId = Guid.NewGuid();
        _connections.GetLineCredentialsAsync(tenantId).Returns(new LineCredentials("tok", "U1"));
        _notifier.PushAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>())
            .Returns(new LineNotifyResult(false, "http_401", "unauthorized"));

        var result = await _sut.ExecuteAsync(tenantId);

        Assert.False(result.Success);
        Assert.Contains("unauthorized", result.ErrorMessage);
    }
}
```

- [ ] **Step 2: Run to confirm FAIL**

```bash
cd /Users/pipat/Documents/MiniProject/Ryla && dotnet test backend/tests/Ryla.Core.Tests/ --filter "TestNotificationUseCaseTests" 2>&1 | tail -5
```
Expected: error — type `TestNotificationUseCase` not found.

- [ ] **Step 3: Create ITestNotificationUseCase port**

Create `backend/src/Ryla.Core/UseCases/ITestNotificationUseCase.cs`:

```csharp
namespace Ryla.Core.UseCases;

public record TestNotificationResult(bool Success, string? ErrorMessage = null);

public interface ITestNotificationUseCase
{
    Task<TestNotificationResult> ExecuteAsync(Guid tenantId, CancellationToken ct = default);
}
```

- [ ] **Step 4: Create TestNotificationUseCase implementation**

Create `backend/src/Ryla.Core/UseCases/TestNotificationUseCase.cs`:

```csharp
using Microsoft.Extensions.Logging;
using Ryla.Core.Ports.Outbound;

namespace Ryla.Core.UseCases;

internal sealed class TestNotificationUseCase(
    IConnectionRepository connectionRepository,
    ILineNotifier lineNotifier,
    ILogger<TestNotificationUseCase> logger) : ITestNotificationUseCase
{
    public async Task<TestNotificationResult> ExecuteAsync(
        Guid tenantId, CancellationToken ct = default)
    {
        var credentials = await connectionRepository.GetLineCredentialsAsync(tenantId, ct);

        if (credentials is null)
        {
            logger.LogWarning("TestNotification: LINE credentials not found for tenant {TenantId}", tenantId);
            return new TestNotificationResult(false, "ไม่พบ LINE connection กรุณาเพิ่ม LINE OA ก่อน");
        }

        var testOrderId = $"TEST-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}";
        var message = $"🧪 ทดสอบ Ryla\n✅ ร้านค้าเชื่อมต่อสำเร็จ!\n📦 Order: {testOrderId}";

        var result = await lineNotifier.PushAsync(
            credentials.ChannelAccessToken,
            credentials.TargetUserId,
            message,
            ct);

        if (!result.Success)
        {
            logger.LogWarning(
                "TestNotification: LINE push failed for tenant {TenantId}: {Error}",
                tenantId, result.ErrorMessage);
            return new TestNotificationResult(false, $"ส่ง LINE ไม่สำเร็จ: {result.ErrorMessage}");
        }

        logger.LogInformation("TestNotification: success for tenant {TenantId}", tenantId);
        return new TestNotificationResult(true);
    }
}
```

- [ ] **Step 5: Register in CoreServiceCollectionExtensions**

Edit `backend/src/Ryla.Core/UseCases/CoreServiceCollectionExtensions.cs` — add registration:

```csharp
using Microsoft.Extensions.DependencyInjection;

namespace Ryla.Core.UseCases;

public static class CoreServiceCollectionExtensions
{
    public static IServiceCollection AddRylaCoreUseCases(this IServiceCollection services)
    {
        services.AddScoped<IProcessOrderWebhookUseCase, ProcessOrderWebhookUseCase>();
        services.AddScoped<ITestNotificationUseCase, TestNotificationUseCase>();
        return services;
    }
}
```

- [ ] **Step 6: Run tests — confirm PASS**

```bash
cd /Users/pipat/Documents/MiniProject/Ryla && dotnet test backend/tests/Ryla.Core.Tests/ --filter "TestNotificationUseCaseTests" 2>&1 | tail -10
```
Expected: 3 tests PASS.

- [ ] **Step 7: Commit**

```bash
git add backend/src/Ryla.Core/UseCases/ITestNotificationUseCase.cs \
        backend/src/Ryla.Core/UseCases/TestNotificationUseCase.cs \
        backend/src/Ryla.Core/UseCases/CoreServiceCollectionExtensions.cs \
        backend/tests/Ryla.Core.Tests/UseCases/TestNotificationUseCaseTests.cs
git commit -m "feat(core): add TestNotificationUseCase for LINE push via test button"
```

---

## Task 11: Backend — Test Notification HTTP Endpoint

**Files:**
- Create: `backend/src/Ryla.Api/Endpoints/TestNotificationEndpoints.cs`
- Modify: `backend/src/Ryla.Api/Extensions/ServiceCollectionExtensions.cs` (add JSON types)
- Modify: `backend/src/Ryla.Api/Program.cs` (map endpoint)

- [ ] **Step 1: Create endpoint file**

Create `backend/src/Ryla.Api/Endpoints/TestNotificationEndpoints.cs`:

```csharp
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Ryla.Core.UseCases;

namespace Ryla.Api.Endpoints;

public record TestNotificationRequest(string TenantId);
public record TestNotificationResponse(bool Success, string Message);

public static class TestNotificationEndpoints
{
    public static IEndpointRouteBuilder MapTestNotificationEndpoints(
        this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/test-notification", async (
            TestNotificationRequest request,
            ITestNotificationUseCase useCase,
            HttpContext ctx,
            IConfiguration config,
            CancellationToken ct) =>
        {
            var expectedSecret = config["InternalApiSecret"];
            if (string.IsNullOrEmpty(expectedSecret)
                || !ctx.Request.Headers.TryGetValue("X-Internal-Secret", out var secret)
                || secret != expectedSecret)
            {
                return Results.Unauthorized();
            }

            if (!Guid.TryParse(request.TenantId, out var tenantId))
                return Results.BadRequest("Invalid tenantId format");

            var result = await useCase.ExecuteAsync(tenantId, ct);

            return Results.Ok(new TestNotificationResponse(
                result.Success,
                result.Success
                    ? "ส่งสำเร็จ! เช็ค LINE ของคุณ"
                    : result.ErrorMessage ?? "เกิดข้อผิดพลาด"));
        });

        return app;
    }
}
```

- [ ] **Step 2: Add new types to RylaJsonContext**

Edit `backend/src/Ryla.Api/Extensions/ServiceCollectionExtensions.cs` — add two `[JsonSerializable]` attributes to `RylaJsonContext`:

Current:
```csharp
[JsonSerializable(typeof(HealthResponse))]
[JsonSerializable(typeof(TikTokWebhookPayload))]
[JsonSerializable(typeof(TikTokOrderContent))]
[JsonSerializable(typeof(ShopeeWebhookPayload))]
[JsonSourceGenerationOptions(...)]
internal partial class RylaJsonContext : JsonSerializerContext { }
```

After (add 2 lines):
```csharp
[JsonSerializable(typeof(HealthResponse))]
[JsonSerializable(typeof(TikTokWebhookPayload))]
[JsonSerializable(typeof(TikTokOrderContent))]
[JsonSerializable(typeof(ShopeeWebhookPayload))]
[JsonSerializable(typeof(TestNotificationRequest))]
[JsonSerializable(typeof(TestNotificationResponse))]
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    WriteIndented = false)]
internal partial class RylaJsonContext : JsonSerializerContext { }
```

- [ ] **Step 3: Map endpoint in Program.cs**

Edit `backend/src/Ryla.Api/Program.cs` — add after `app.MapShopeeWebhookEndpoints()`:

```csharp
app.MapTestNotificationEndpoints();
```

Full Program.cs after edit:
```csharp
using Ryla.Api.Endpoints;
using Ryla.Api.Extensions;
using Ryla.Infrastructure;

var builder = WebApplication.CreateSlimBuilder(args);

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.TypeInfoResolverChain.Insert(0, RylaJsonContext.Default);
});

builder.Services.AddOpenApi();
builder.Services.AddRylaCoreServices(builder.Configuration);
builder.Services.AddRylaInfrastructureServices(builder.Configuration);

var app = builder.Build();

app.MapOpenApi();
app.MapHealthEndpoints();
app.MapTikTokWebhookEndpoints();
app.MapShopeeWebhookEndpoints();
app.MapTestNotificationEndpoints();

app.Run();
```

- [ ] **Step 4: Build backend to confirm no AOT issues**

```bash
cd /Users/pipat/Documents/MiniProject/Ryla && dotnet build backend/src/Ryla.Api/ 2>&1 | grep -E "error|warning IL" | head -10
```
Expected: no errors, no AOT/IL warnings.

- [ ] **Step 5: Commit**

```bash
git add backend/src/Ryla.Api/Endpoints/TestNotificationEndpoints.cs \
        backend/src/Ryla.Api/Extensions/ServiceCollectionExtensions.cs \
        backend/src/Ryla.Api/Program.cs
git commit -m "feat(api): add POST /api/test-notification endpoint with internal-secret auth"
```

> `appsettings.Development.json` was already committed in Task 2 — do not re-stage it here.

---

## Task 12: Frontend — Test Notification API Route

**Files:**
- Create: `frontend/src/app/api/test-notification/route.ts`

This Next.js API route:
1. Validates the Supabase session from cookies
2. Looks up the user's `tenant_id` from the `profiles` table
3. Calls `POST http://localhost:5282/api/test-notification` with `X-Internal-Secret`
4. Returns the backend response to the browser

- [ ] **Step 1: Create API route**

Create `frontend/src/app/api/test-notification/route.ts`:

```typescript
import { NextResponse } from 'next/server'
import { createClient } from '@/lib/supabase/server'

export async function POST() {
  const supabase = await createClient()

  const {
    data: { user },
  } = await supabase.auth.getUser()

  if (!user) {
    return NextResponse.json(
      { success: false, message: 'Unauthorized' },
      { status: 401 },
    )
  }

  const { data: profile } = await supabase
    .from('profiles')
    .select('tenant_id')
    .eq('id', user.id)
    .single()

  if (!profile) {
    return NextResponse.json(
      { success: false, message: 'Profile not found' },
      { status: 404 },
    )
  }

  const backendUrl = process.env['NEXT_PUBLIC_BACKEND_URL'] ?? 'http://localhost:5282'
  const internalSecret = process.env['INTERNAL_API_SECRET'] ?? ''

  try {
    const res = await fetch(`${backendUrl}/api/test-notification`, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        'X-Internal-Secret': internalSecret,
      },
      body: JSON.stringify({ tenantId: profile.tenant_id }),
    })

    const json = (await res.json()) as { success: boolean; message: string }
    return NextResponse.json(json, { status: res.ok ? 200 : res.status })
  } catch {
    return NextResponse.json(
      { success: false, message: 'Backend unreachable' },
      { status: 502 },
    )
  }
}
```

- [ ] **Step 2: Verify TypeScript**

```bash
cd frontend && npx tsc --noEmit
```
Expected: no errors.

- [ ] **Step 3: Commit**

```bash
git add frontend/src/app/api/test-notification/route.ts
git commit -m "feat(frontend): add /api/test-notification proxy route → .NET backend"
```

---

## Task 14: E2E Smoke Test — Backend Endpoint

**Files:**
- Create: `tests/e2e/test_notification_api.py`

Tests `POST /api/test-notification` directly. Frontend E2E (Playwright) is deferred per spec.

The test tenant `11111111-1111-1111-1111-111111111111` with LINE credentials must be seeded (already present from real-world validation in local DB).

- [ ] **Step 1: Write E2E test**

Create `tests/e2e/test_notification_api.py`:

```python
#!/usr/bin/env python3
"""
E2E smoke tests for POST /api/test-notification

Tests (in order):
  1. Missing secret → 401
  2. Wrong secret → 401
  3. Invalid tenantId format → 400
  4. Valid tenant with LINE connection → 200 + success=true
  5. Unknown tenant (no LINE connection) → 200 + success=false

Pre-requisites:
  - App running at localhost:5282 (make dev && dotnet run)
  - Seed tenant 11111111-1111-1111-1111-111111111111 with line_oa connection
"""

import json
import sys
import urllib.error
import urllib.request

BASE_URL = "http://localhost:5282"
ENDPOINT = f"{BASE_URL}/api/test-notification"
VALID_TENANT_ID = "11111111-1111-1111-1111-111111111111"
UNKNOWN_TENANT_ID = "99999999-9999-9999-9999-999999999999"
INTERNAL_SECRET = "ryla-dev-internal-secret"


def post(payload: dict, secret: str | None) -> tuple[int, dict]:
    body = json.dumps(payload).encode()
    headers = {"Content-Type": "application/json"}
    if secret is not None:
        headers["X-Internal-Secret"] = secret
    req = urllib.request.Request(ENDPOINT, data=body, headers=headers, method="POST")
    try:
        with urllib.request.urlopen(req) as resp:
            return resp.status, json.loads(resp.read())
    except urllib.error.HTTPError as e:
        return e.code, {}


def run_tests() -> bool:
    results: list[tuple[bool, str]] = []

    def test(name: str, ok: bool, detail: str = "") -> None:
        results.append((ok, f"{name}{': ' + detail if detail else ''}"))
        if not ok:
            print(f"  FAIL: {name} — {detail}", file=sys.stderr)

    # 1. Missing secret → 401
    code, _ = post({"tenantId": VALID_TENANT_ID}, secret=None)
    test("missing secret → 401", code == 401, f"got {code}")

    # 2. Wrong secret → 401
    code, _ = post({"tenantId": VALID_TENANT_ID}, secret="wrong-secret")
    test("wrong secret → 401", code == 401, f"got {code}")

    # 3. Invalid tenantId format → 400
    code, _ = post({"tenantId": "not-a-uuid"}, secret=INTERNAL_SECRET)
    test("invalid tenantId → 400", code == 400, f"got {code}")

    # 4. Valid tenant → 200 + success=true
    code, body = post({"tenantId": VALID_TENANT_ID}, secret=INTERNAL_SECRET)
    test("valid tenant → 200", code == 200, f"got {code}")
    test("success=true", body.get("success") is True, str(body))
    test("message present", bool(body.get("message")), str(body))

    # 5. Unknown tenant → 200 + success=false (no LINE connection)
    code, body = post({"tenantId": UNKNOWN_TENANT_ID}, secret=INTERNAL_SECRET)
    test("unknown tenant → 200 (graceful)", code == 200, f"got {code}")
    test("success=false for unknown tenant", body.get("success") is False, str(body))

    passed = sum(1 for ok, _ in results if ok)
    print(f"\nResults: {passed}/{len(results)} passed")
    for ok, name in results:
        print(f"  [{'PASS' if ok else 'FAIL'}] {name}")

    return passed == len(results)


if __name__ == "__main__":
    ok = run_tests()
    sys.exit(0 if ok else 1)
```

- [ ] **Step 2: Verify app is running and run E2E**

```bash
# Ensure backend is running
curl -sf http://localhost:5282/health || echo "Start backend first: make dev && dotnet run --project backend/src/Ryla.Api"

# Run the specific test
python3 tests/e2e/test_notification_api.py
```
Expected: `Results: 7/7 passed`

- [ ] **Step 3: Run full E2E suite**

```bash
make test-e2e
```
Expected: `✅ All E2E tests passed`

- [ ] **Step 4: Commit**

```bash
git add tests/e2e/test_notification_api.py
git commit -m "test(e2e): add smoke tests for POST /api/test-notification endpoint"
```

---

## Task 13: Changelog Fragment + Run Full Test Suite

**Files:**
- Create: `changelog/fragments/9-ryla-6-flow-configuration-ui.yml`

- [ ] **Step 1: Create changelog fragment**

Create `changelog/fragments/9-ryla-6-flow-configuration-ui.yml`:

```yaml
pr: 9  # TBD — update with actual PR number when creating the PR
type: feat
scope: "frontend"
title: "Flow Configuration UI — connect LINE OA + Google Sheets"
description: |
  Dashboard ที่ให้ seller login ด้วย Google, เพิ่ม LINE OA / Google Sheets connection,
  กด "ส่งทดสอบ" เพื่อรับ LINE message จริง และดู Webhook URL สำหรับ Shopee
breaking: false
migration: true
```

- [ ] **Step 2: Run full backend test suite**

```bash
dotnet test backend/ 2>&1 | tail -15
```
Expected: all tests pass.

- [ ] **Step 3: Run frontend tests**

```bash
cd frontend && npx vitest run 2>&1 | tail -10
```
Expected: all tests pass.

- [ ] **Step 4: Run type check**

```bash
cd frontend && npx tsc --noEmit 2>&1
```
Expected: no errors.

- [ ] **Step 5: Commit**

```bash
git add changelog/fragments/9-ryla-6-flow-configuration-ui.yml
git commit -m "chore: add changelog fragment for RYLA-6 flow configuration UI"
```

---

## Task 15: Document + Active Context Update

**Files:**
- Modify: `.claude/memory/active-context.md`

- [ ] **Step 1: Update active-context.md**

Update `.claude/memory/active-context.md`:
- Move RYLA-6 from "Next Up" to "Last 3 Completed Tasks"
- Update "Next Up" to: RYLA-7 (Azure Functions deploy / Shopee real validation when business account ready)

- [ ] **Step 2: Final manual verification**

Start all services and do the golden path manually:

```bash
make dev
dotnet run --project backend/src/Ryla.Api/Ryla.Api.csproj &
cd frontend && npm run dev
```

Open http://localhost:3000:
- [ ] `GET /login` renders Google sign-in button
- [ ] Google OAuth flow completes, redirects to `/connections`
- [ ] Add LINE OA via form → card shows "Connected"
- [ ] "ส่งทดสอบ" → toast "✅ ส่งสำเร็จ! เช็ค LINE ของคุณ" + LINE message received
- [ ] Add Google Sheets via drag & drop JSON → card shows "Connected"
- [ ] Delete connection → card removed
- [ ] Navigate to `/webhook` → URL displayed with copy button
- [ ] Open incognito → `/connections` redirects to `/login`

- [ ] **Step 3: Commit**

```bash
git add .claude/memory/active-context.md
git commit -m "docs: update active-context — RYLA-6 complete"
```

---

## Spec Coverage Check

| Spec Requirement | Covered by Task |
|-----------------|-----------------|
| Google OAuth login | Task 3 |
| Dashboard layout (sidebar + main) | Task 4 |
| Connections list page | Task 6 |
| Add LINE OA form | Task 7 |
| Add Google Sheets (drag & drop) | Task 8 |
| Edit connection (pre-filled form) | Task 8 ([platform]/page.tsx server-side pre-fill) |
| Delete connection | Task 6 (ConnectionCard) |
| ส่งทดสอบ button | Task 6, 10, 11, 12 |
| Webhook URL display | Task 9 |
| Middleware redirect if not logged in | Task 3 (proxy.ts) |
| RLS tenant isolation | Task 1 + existing init migration |
| E2E smoke test | Task 14 |
