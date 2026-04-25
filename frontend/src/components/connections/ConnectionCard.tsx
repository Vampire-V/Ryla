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
