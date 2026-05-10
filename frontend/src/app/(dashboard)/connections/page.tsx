import Link from 'next/link'
import { ShoppingBag } from 'lucide-react'
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
          <h1 className="text-2xl font-bold text-slate-900">Connections</h1>
          <p className="mt-1 text-sm text-slate-500">เชื่อมต่อ platform เพื่อรับการแจ้งเตือน</p>
        </div>
        <div className="flex gap-2">
          {!lineConn && (
            <Link
              href="/connections/new/line"
              className="rounded-lg bg-indigo-600 px-3 py-2 text-sm font-medium text-white hover:bg-indigo-700"
            >
              + เพิ่ม LINE OA
            </Link>
          )}
          {!sheetsConn && (
            <Link
              href="/connections/new/google-sheets"
              className="rounded-lg bg-indigo-600 px-3 py-2 text-sm font-medium text-white hover:bg-indigo-700"
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
          <div className="flex h-9 w-9 flex-shrink-0 items-center justify-center rounded-lg bg-orange-500 text-white"><ShoppingBag size={18} /></div>
          <div className="flex-1">
            <div className="font-semibold text-slate-900">Shopee</div>
            <div className="text-xs text-slate-400">ต้องมี Partner Key จาก Shopee Open Platform</div>
          </div>
          <span className="rounded-full bg-slate-100 px-2 py-0.5 text-xs text-slate-400 font-medium">
            ยังไม่เชื่อมต่อ
          </span>
        </div>
      </div>
    </div>
  )
}
