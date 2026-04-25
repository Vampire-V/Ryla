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
