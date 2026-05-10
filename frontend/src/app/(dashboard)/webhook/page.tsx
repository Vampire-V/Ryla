'use client'

import { useCallback, useEffect, useRef, useState } from 'react'
import { Copy, Check, Webhook, FlaskConical } from 'lucide-react'
import { SimulatorButton } from '@/components/webhook/SimulatorButton'

export default function WebhookPage() {
  const [webhookUrl] = useState(() =>
    typeof window !== 'undefined' ? `${window.location.origin}/webhooks/shopee` : ''
  )
  const [copied, setCopied] = useState(false)
  const timerRef = useRef<ReturnType<typeof setTimeout> | null>(null)

  useEffect(() => {
    return () => {
      if (timerRef.current) clearTimeout(timerRef.current)
    }
  }, [])

  const handleCopy = useCallback(async () => {
    if (!webhookUrl) return
    try {
      await navigator.clipboard.writeText(webhookUrl)
      setCopied(true)
      if (timerRef.current) clearTimeout(timerRef.current)
      timerRef.current = setTimeout(() => setCopied(false), 2000)
    } catch {
      // clipboard access denied or unavailable — do nothing
    }
  }, [webhookUrl])

  return (
    <div>
      <h1 className="mb-1 text-2xl font-bold text-slate-900">Webhook URL</h1>
      <p className="mb-6 text-sm text-slate-500">
        นำ URL นี้ไปตั้งค่าใน Shopee Open Platform → Webhook Settings
      </p>

      <div className="max-w-xl rounded-xl border border-slate-200 bg-white p-5 shadow-sm">
        <div className="mb-3 text-xs font-semibold uppercase tracking-wide text-slate-400">
          <Webhook size={14} className="mr-1.5 inline" />Shopee Webhook URL
        </div>
        <div className="flex items-center gap-2">
          <code className="flex-1 rounded-md bg-slate-50 px-3 py-2 text-sm text-slate-800 break-all">
            {webhookUrl || 'กำลังโหลด...'}
          </code>
          <button
            type="button"
            onClick={handleCopy}
            disabled={!webhookUrl}
            className="flex-shrink-0 rounded-md border border-slate-200 px-3 py-2 text-sm text-slate-600 hover:bg-slate-50 transition-colors disabled:opacity-40"
          >
            {copied ? <><Check size={14} className="mr-1 inline" />Copied</> : <><Copy size={14} className="mr-1 inline" />Copy</>}
          </button>
        </div>

        <div className="mt-4 rounded-md bg-amber-50 p-3 text-xs text-amber-800">
          <p className="font-semibold">วิธีตั้งค่าใน Shopee Open Platform:</p>
          <div className="mt-2 space-y-1.5">
            {[
              'เข้า Shopee Open Platform → My Apps → [app ของคุณ]',
              'ไปที่ Webhook Settings',
              'วาง URL ด้านบนใน Order Status Update URL',
              'กด Save และรอ Shopee ยืนยัน',
            ].map((step, i) => (
              <div key={i} className="flex items-start gap-2">
                <span className="flex h-4 w-4 flex-shrink-0 items-center justify-center rounded-full bg-amber-200 text-[10px] font-bold text-amber-800">
                  {i + 1}
                </span>
                <span>{step}</span>
              </div>
            ))}
          </div>
        </div>
      </div>

      {/* Simulator section */}
      <div className="mt-8 max-w-xl">
        <div className="mb-1 text-xs font-semibold uppercase tracking-wide text-slate-400">
          <FlaskConical size={14} className="mr-1.5 inline" />ทดสอบระบบ
        </div>
        <div className="rounded-xl border border-slate-200 bg-white p-5 shadow-sm">
          <p className="mb-1 text-sm font-medium text-slate-800">จำลองออเดอร์ใหม่</p>
          <p className="mb-4 text-xs text-slate-500">
            กดปุ่มด้านล่างเพื่อจำลองว่ามีออเดอร์ใหม่เข้ามาทาง URL นี้
            แล้วตรวจสอบว่า LINE และ Sheets ได้รับข้อมูลถูกต้อง
          </p>
          <SimulatorButton platform="tiktok_shop" />
        </div>
      </div>
    </div>
  )
}
