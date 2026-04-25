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
