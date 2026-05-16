import Link from 'next/link'
import { redirect } from 'next/navigation'
import { createClient } from '@/lib/supabase/server'
import type { ProfitSummary } from '@/types/profit'

// searchParams เป็น Promise ใน Next.js 16 (breaking change จาก v14)
type SearchParams = Promise<{ range?: string }>

const VALID_RANGES = ['7d', '30d', '90d'] as const
type Range = (typeof VALID_RANGES)[number]

function formatTHB(value: number): string {
  return new Intl.NumberFormat('th-TH', { style: 'currency', currency: 'THB' }).format(value)
}

function formatPercent(value: number | null): string {
  if (value === null) return '—'
  return (value * 100).toFixed(1) + '%'
}

interface KpiCardProps {
  label: string
  value: string
  sub?: string
  highlight?: boolean
}

function KpiCard({ label, value, sub, highlight }: KpiCardProps) {
  return (
    <div
      className={`rounded-xl border p-5 shadow-sm ${
        highlight ? 'border-indigo-200 bg-indigo-50' : 'border-slate-200 bg-white'
      }`}
    >
      <p className="text-xs font-semibold uppercase tracking-wide text-slate-400">{label}</p>
      <p className={`mt-2 text-2xl font-bold ${highlight ? 'text-indigo-700' : 'text-slate-900'}`}>
        {value}
      </p>
      {sub && <p className="mt-1 text-xs text-slate-500">{sub}</p>}
    </div>
  )
}

export default async function ProfitPage({ searchParams }: { searchParams: SearchParams }) {
  const sp = await searchParams
  const range: Range = VALID_RANGES.includes(sp.range as Range)
    ? (sp.range as Range)
    : '30d'

  const supabase = await createClient()
  // getUser() ทำ server-side JWT verification กับ Supabase auth server
  // getSession() ดึง access_token เพื่อส่งไป backend (ไม่ re-verify แต่ใช้ค่าจาก cookie)
  const {
    data: { user },
  } = await supabase.auth.getUser()
  if (!user) redirect('/login')

  const { data: { session } } = await supabase.auth.getSession()
  if (!session) redirect('/login')
  // Backend resolve tenant_id จาก JWT sub claim — ไม่ใช้ X-Tenant-Id header (แก้ IDOR)

  const apiUrl = process.env['NEXT_PUBLIC_API_URL'] ?? ''

  let summary: ProfitSummary | null = null
  let fetchError = false

  try {
    const res = await fetch(
      `${apiUrl}/api/profit/summary?range=${range}`,
      {
        headers: { 'Authorization': `Bearer ${session.access_token}` },
        cache: 'no-store',
      },
    )
    if (res.ok) {
      summary = (await res.json()) as ProfitSummary
    } else {
      fetchError = true
    }
  } catch {
    fetchError = true
  }

  const isEmpty = summary !== null && summary.completedOrderCount === 0

  return (
    <div>
      {/* Header */}
      <div className="mb-6 flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-bold text-slate-900">Profit Dashboard</h1>
          <p className="mt-1 text-sm text-slate-500">ภาพรวมรายได้และ margin จากออเดอร์ Shopee</p>
        </div>

        {/* Date range selector */}
        <div className="flex gap-1 rounded-lg border border-slate-200 bg-white p-1 shadow-sm">
          {VALID_RANGES.map((r) => (
            <Link
              key={r}
              href={`/profit?range=${r}`}
              className={`rounded-md px-3 py-1.5 text-xs font-medium transition-colors ${
                range === r
                  ? 'bg-indigo-600 text-white shadow-sm'
                  : 'text-slate-500 hover:bg-slate-50 hover:text-slate-700'
              }`}
            >
              {r}
            </Link>
          ))}
        </div>
      </div>

      {/* Error state */}
      {fetchError && (
        <div className="rounded-xl border border-red-200 bg-red-50 p-5 text-sm text-red-700">
          ไม่สามารถโหลดข้อมูลได้ในขณะนี้ กรุณาลองใหม่ภายหลัง
        </div>
      )}

      {/* Empty state */}
      {!fetchError && isEmpty && (
        <div className="flex flex-col items-center justify-center rounded-xl border border-dashed border-slate-300 bg-white py-16 text-center shadow-sm">
          <p className="mb-2 text-lg font-semibold text-slate-700">ยังไม่มีข้อมูลออเดอร์</p>
          <p className="mb-6 text-sm text-slate-400">
            เชื่อมต่อ Shopee เพื่อดึงออเดอร์และคำนวณ margin
          </p>
          <Link
            href="/connections/shopee"
            className="rounded-lg bg-orange-500 px-5 py-2.5 text-sm font-semibold text-white hover:bg-orange-600 transition-colors"
          >
            เชื่อมต่อ Shopee เพื่อดู margin จริง
          </Link>
        </div>
      )}

      {/* KPI cards */}
      {!fetchError && summary !== null && !isEmpty && (
        <>
          <div className="mb-4 grid grid-cols-1 gap-4 sm:grid-cols-3">
            <KpiCard
              label="Total Revenue"
              value={formatTHB(summary.totalRevenue)}
              highlight
            />
            <KpiCard
              label="Gross Margin"
              value={formatPercent(summary.grossMarginPercent)}
              sub={`รวม ${formatTHB(summary.totalGrossMargin)}`}
            />
            <KpiCard
              label="Net Margin"
              value={formatPercent(summary.netMarginPercent)}
              sub={
                summary.netMarginPercent === null
                  ? 'กรอก COGS เพื่อดู net margin'
                  : summary.totalNetMargin !== null
                    ? `รวม ${formatTHB(summary.totalNetMargin)}`
                    : undefined
              }
            />
          </div>

          {/* Pending note */}
          {summary.pendingSettlementCount > 0 && (
            <div className="mb-4 rounded-lg bg-amber-50 px-4 py-2.5 text-xs text-amber-800">
              ไม่รวมออเดอร์ที่รอยืนยัน{' '}
              <span className="font-semibold">{summary.pendingSettlementCount} รายการ</span>
            </div>
          )}

          {/* Quick links */}
          <div className="flex gap-3">
            <Link
              href={`/profit/orders?range=${range}`}
              className="rounded-lg border border-slate-200 bg-white px-4 py-2 text-sm font-medium text-slate-600 hover:bg-slate-50 transition-colors"
            >
              ดูรายการออเดอร์ทั้งหมด →
            </Link>
            <Link
              href="/profit/sku-costs"
              className="rounded-lg border border-slate-200 bg-white px-4 py-2 text-sm font-medium text-slate-600 hover:bg-slate-50 transition-colors"
            >
              จัดการ COGS →
            </Link>
          </div>
        </>
      )}
    </div>
  )
}
