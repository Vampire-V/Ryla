'use client'

import { useRef, useState } from 'react'
import { useRouter } from 'next/navigation'
import { getTenantId, upsertSheetsConnection } from '@/lib/connections'
import { Sheet, Upload, CheckCircle2 } from 'lucide-react'

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
        <div className="flex h-9 w-9 items-center justify-center rounded-lg bg-blue-600 text-white">
          <Sheet size={20} />
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
              <CheckCircle2 size={28} className="text-green-600" />
              <p className="mt-1 text-sm font-semibold text-green-700">โหลดสำเร็จ</p>
              <p className="mt-0.5 text-xs text-slate-500 break-all">{parsedEmail}</p>
            </>
          ) : (
            <>
              <Upload size={28} className="text-indigo-500" />
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
        <p className="mt-1 text-xs text-slate-500">
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
        <p className="mt-1 text-xs text-slate-500">
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
        <p className="mt-1 text-xs text-slate-500">ค่า default: Orders</p>
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
