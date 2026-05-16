import Link from 'next/link'
import { redirect } from 'next/navigation'
import { createClient } from '@/lib/supabase/server'
import { SkuCostTable } from '@/components/profit/SkuCostForm'
import type { SkuCostsResponse } from '@/types/profit'

// Server Component — fetch list แล้วส่งให้ SkuCostTable (client)
export default async function SkuCostsPage() {
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

  let result: SkuCostsResponse | null = null
  let fetchError = false

  try {
    const res = await fetch(`${apiUrl}/api/profit/sku-costs`, {
      headers: { 'X-Tenant-Id': tenantId },
      cache: 'no-store',
    })
    if (res.ok) {
      result = (await res.json()) as SkuCostsResponse
    } else {
      fetchError = true
    }
  } catch {
    fetchError = true
  }

  return (
    <div>
      {/* Header */}
      <div className="mb-6 flex items-start justify-between">
        <div>
          <div className="flex items-center gap-2 mb-1">
            <Link
              href="/profit"
              className="text-sm text-slate-400 hover:text-slate-600 transition-colors"
            >
              Profit Dashboard
            </Link>
            <span className="text-slate-300">/</span>
            <span className="text-sm font-medium text-slate-700">จัดการ COGS</span>
          </div>
          <h1 className="text-2xl font-bold text-slate-900">จัดการต้นทุนสินค้า (COGS)</h1>
          <p className="mt-1 text-sm text-slate-500">
            กรอกต้นทุนต่อชิ้นเพื่อให้ระบบคำนวณ net margin ได้อย่างแม่นยำ
          </p>
        </div>
      </div>

      {/* Info note */}
      <div className="mb-4 rounded-lg bg-indigo-50 border border-indigo-100 px-4 py-3 text-sm text-indigo-700">
        <span className="font-semibold">COGS</span> คือต้นทุนต่อหน่วยสินค้า (Cost of Goods Sold) —
        ใช้คำนวณ gross margin ก่อนหักค่าใช้จ่ายอื่น
      </div>

      {/* Error state */}
      {fetchError && (
        <div className="rounded-xl border border-red-200 bg-red-50 p-5 text-sm text-red-700">
          ไม่สามารถโหลดข้อมูลได้ในขณะนี้ กรุณาลองใหม่ภายหลัง
        </div>
      )}

      {/* SKU cost table with inline edit */}
      {!fetchError && (
        <SkuCostTable items={result?.items ?? []} tenantId={tenantId} />
      )}
    </div>
  )
}
