'use client'

import { useState } from 'react'
import { PLATFORM_LABELS, type SimulatorPlatform } from '@/lib/simulator/platforms'

type SimulateResult = {
  lineSuccess: boolean
  sheetsSuccess: boolean
  orderId: string
  lineError: string | null
  sheetsError: string | null
}

type Props = {
  platform: SimulatorPlatform
}

export function SimulatorButton({ platform }: Props) {
  const [loading, setLoading] = useState(false)
  const [result, setResult] = useState<SimulateResult | null>(null)

  const handleSimulate = async () => {
    setLoading(true)
    setResult(null)
    try {
      const res = await fetch('/api/simulate-order', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ platform }),
      })
      const json = (await res.json()) as SimulateResult
      setResult(json)
    } catch {
      setResult({ lineSuccess: false, sheetsSuccess: false, orderId: '', lineError: 'ไม่สามารถเชื่อมต่อได้', sheetsError: null })
    } finally {
      setLoading(false)
    }
  }

  return (
    <div>
      <button
        type="button"
        onClick={handleSimulate}
        disabled={loading}
        className="w-full rounded-md bg-indigo-600 px-4 py-2.5 text-sm font-medium text-white hover:bg-indigo-700 disabled:opacity-50 transition-colors"
      >
        {loading ? 'กำลังจำลอง...' : `จำลองออเดอร์ใหม่ — ${PLATFORM_LABELS[platform]}`}
      </button>

      {result && (
        <div className="mt-3 rounded-lg border border-slate-200 bg-slate-50 p-3 text-sm">
          {result.orderId && (
            <p className="mb-2 font-mono text-xs text-slate-500">Order: {result.orderId}</p>
          )}
          <div className="space-y-1">
            <div className="flex items-center gap-2">
              <span>{result.lineSuccess ? '✅' : '❌'}</span>
              <span className="text-slate-700">
                LINE: {result.lineSuccess ? 'ส่งสำเร็จ เช็ค LINE ของคุณ' : (result.lineError ?? 'เกิดข้อผิดพลาด')}
              </span>
            </div>
            <div className="flex items-center gap-2">
              <span>{result.sheetsSuccess ? '✅' : '⚠️'}</span>
              <span className="text-slate-700">
                Sheets: {result.sheetsSuccess ? 'บันทึกแล้ว' : (result.sheetsError ?? 'เกิดข้อผิดพลาด')}
              </span>
            </div>
          </div>
        </div>
      )}
    </div>
  )
}
