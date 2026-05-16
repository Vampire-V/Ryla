import Link from 'next/link'
import { redirect } from 'next/navigation'
import { createClient } from '@/lib/supabase/server'
import type { OrdersResponse, ShopeeOrder } from '@/types/profit'

// searchParams เป็น Promise ใน Next.js 16 (breaking change จาก v14)
type SearchParams = Promise<{ range?: string; page?: string }>

const VALID_RANGES = ['7d', '30d', '90d'] as const
type Range = (typeof VALID_RANGES)[number]

const PAGE_SIZE = 50

function formatTHB(value: number | null): string {
  if (value === null) return '—'
  return new Intl.NumberFormat('th-TH', { style: 'currency', currency: 'THB' }).format(value)
}

function formatDate(value: string | null): string {
  if (!value) return '—'
  return new Intl.DateTimeFormat('th-TH', {
    dateStyle: 'short',
    timeStyle: 'short',
  }).format(new Date(value))
}

function StatusBadge({ status }: { status: string }) {
  const isPending = status !== 'COMPLETED'
  return (
    <span
      className={`inline-flex items-center rounded-full px-2 py-0.5 text-xs font-medium ${
        isPending
          ? 'bg-amber-100 text-amber-800'
          : 'bg-emerald-100 text-emerald-700'
      }`}
    >
      {isPending ? 'รอยืนยัน' : 'สำเร็จ'}
    </span>
  )
}

function OrderRow({ order }: { order: ShopeeOrder }) {
  return (
    <tr className="border-b border-slate-100 hover:bg-slate-50/50 transition-colors">
      <td className="py-3 pl-4 pr-3 text-xs font-mono text-slate-700">{order.orderSn}</td>
      <td className="px-3 py-3">
        <StatusBadge status={order.status} />
      </td>
      <td className="px-3 py-3 text-right text-sm text-slate-700">{formatTHB(order.revenue)}</td>
      <td className="px-3 py-3 text-right text-sm text-slate-700">
        {formatTHB(order.grossMargin)}
      </td>
      <td className="px-3 py-3 text-right text-sm text-slate-700">
        {formatTHB(order.netMargin)}
      </td>
      <td className="px-3 py-3 pr-4 text-right text-xs text-slate-400">
        {formatDate(order.syncedAt)}
      </td>
    </tr>
  )
}

export default async function OrdersPage({ searchParams }: { searchParams: SearchParams }) {
  const sp = await searchParams
  const range: Range = VALID_RANGES.includes(sp.range as Range)
    ? (sp.range as Range)
    : '30d'
  const page = Math.max(1, parseInt(sp.page ?? '1', 10) || 1)

  const supabase = await createClient()
  const {
    data: { user },
  } = await supabase.auth.getUser()
  if (!user) redirect('/login')

  const { data: profile } = await supabase
    .from('profiles')
    .select('tenant_id')
    .eq('id', user.id)
    .single()

  const tenantId = profile?.tenant_id
  if (!tenantId) redirect('/login')

  const apiUrl = process.env['NEXT_PUBLIC_API_URL'] ?? ''

  let result: OrdersResponse | null = null
  let fetchError = false

  try {
    const res = await fetch(
      `${apiUrl}/api/profit/orders?range=${range}&page=${page}&limit=${PAGE_SIZE}`,
      {
        headers: { 'X-Tenant-Id': tenantId },
        cache: 'no-store',
      },
    )
    if (res.ok) {
      result = (await res.json()) as OrdersResponse
    } else {
      fetchError = true
    }
  } catch {
    fetchError = true
  }

  const totalPages = result ? Math.ceil(result.totalCount / PAGE_SIZE) : 0

  return (
    <div>
      {/* Header */}
      <div className="mb-6 flex items-center justify-between">
        <div>
          <div className="flex items-center gap-2 mb-1">
            <Link
              href={`/profit?range=${range}`}
              className="text-sm text-slate-400 hover:text-slate-600 transition-colors"
            >
              Profit Dashboard
            </Link>
            <span className="text-slate-300">/</span>
            <span className="text-sm font-medium text-slate-700">รายการออเดอร์</span>
          </div>
          <h1 className="text-2xl font-bold text-slate-900">รายการออเดอร์</h1>
          {result && (
            <p className="mt-1 text-sm text-slate-500">
              ทั้งหมด {result.totalCount.toLocaleString('th-TH')} รายการ
            </p>
          )}
        </div>

        {/* Date range selector */}
        <div className="flex gap-1 rounded-lg border border-slate-200 bg-white p-1 shadow-sm">
          {VALID_RANGES.map((r) => (
            <Link
              key={r}
              href={`/profit/orders?range=${r}&page=1`}
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
      {!fetchError && result?.orders.length === 0 && (
        <div className="flex flex-col items-center justify-center rounded-xl border border-dashed border-slate-300 bg-white py-16 text-center">
          <p className="mb-2 text-lg font-semibold text-slate-700">ไม่มีออเดอร์ในช่วงเวลานี้</p>
          <p className="text-sm text-slate-400">ลองเปลี่ยนช่วงเวลาด้านบน</p>
        </div>
      )}

      {/* Table */}
      {!fetchError && result && result.orders.length > 0 && (
        <>
          <div className="rounded-xl border border-slate-200 bg-white shadow-sm overflow-hidden">
            <table className="min-w-full">
              <thead>
                <tr className="border-b border-slate-200 bg-slate-50">
                  <th className="py-3 pl-4 pr-3 text-left text-xs font-semibold uppercase tracking-wide text-slate-400">
                    Order SN
                  </th>
                  <th className="px-3 py-3 text-left text-xs font-semibold uppercase tracking-wide text-slate-400">
                    Status
                  </th>
                  <th className="px-3 py-3 text-right text-xs font-semibold uppercase tracking-wide text-slate-400">
                    Revenue
                  </th>
                  <th className="px-3 py-3 text-right text-xs font-semibold uppercase tracking-wide text-slate-400">
                    Gross Margin
                  </th>
                  <th className="px-3 py-3 text-right text-xs font-semibold uppercase tracking-wide text-slate-400">
                    Net Margin
                  </th>
                  <th className="px-3 py-3 pr-4 text-right text-xs font-semibold uppercase tracking-wide text-slate-400">
                    Synced At
                  </th>
                </tr>
              </thead>
              <tbody>
                {result.orders.map((order) => (
                  <OrderRow key={order.id} order={order} />
                ))}
              </tbody>
            </table>
          </div>

          {/* Pagination */}
          {totalPages > 1 && (
            <div className="mt-4 flex items-center justify-between">
              <p className="text-xs text-slate-400">
                หน้า {page} จาก {totalPages}
              </p>
              <div className="flex gap-2">
                {page > 1 && (
                  <Link
                    href={`/profit/orders?range=${range}&page=${page - 1}`}
                    className="rounded-lg border border-slate-200 bg-white px-3 py-1.5 text-xs font-medium text-slate-600 hover:bg-slate-50 transition-colors"
                  >
                    ← ก่อนหน้า
                  </Link>
                )}
                {page < totalPages && (
                  <Link
                    href={`/profit/orders?range=${range}&page=${page + 1}`}
                    className="rounded-lg border border-slate-200 bg-white px-3 py-1.5 text-xs font-medium text-slate-600 hover:bg-slate-50 transition-colors"
                  >
                    ถัดไป →
                  </Link>
                )}
              </div>
            </div>
          )}
        </>
      )}
    </div>
  )
}
