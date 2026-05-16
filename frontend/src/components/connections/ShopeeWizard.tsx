'use client'

// "use client" จำเป็น: ใช้ useSearchParams, useRouter, polling interval

import { useEffect, useRef, useState } from 'react'
import { useRouter, useSearchParams } from 'next/navigation'
import Link from 'next/link'
import { ShoppingBag, CheckCircle, AlertCircle, Loader2, ArrowRight } from 'lucide-react'
import type { SyncProgress } from '@/types/profit'

interface ShopeeWizardProps {
  tenantId: string
  authorizeUrl: string
}

const POLL_INTERVAL_MS = 3000

export function ShopeeWizard({ tenantId, authorizeUrl }: ShopeeWizardProps) {
  const searchParams = useSearchParams()
  const router = useRouter()

  const rawStep = searchParams.get('step')
  const step = rawStep === '2' ? 2 : rawStep === '3' ? 3 : 1
  const hasOAuthError = searchParams.get('error') === 'oauth_failed'

  const [progress, setProgress] = useState<SyncProgress | null>(null)
  const [pollError, setPollError] = useState(false)
  const intervalRef = useRef<ReturnType<typeof setInterval> | null>(null)

  // เริ่ม polling เมื่ออยู่ใน step 2
  useEffect(() => {
    if (step !== 2) return

    const poll = async () => {
      try {
        const res = await fetch(
          `${process.env['NEXT_PUBLIC_API_URL']}/api/profit/sync-status`,
          { headers: { 'X-Tenant-Id': tenantId }, cache: 'no-store' },
        )
        if (!res.ok) {
          setPollError(true)
          return
        }
        const data = (await res.json()) as SyncProgress
        setProgress(data)

        // เมื่อ sync เสร็จครบให้ไป step 3
        if (data.total > 0 && data.synced >= data.total) {
          if (intervalRef.current) clearInterval(intervalRef.current)
          router.replace('/connections/shopee?step=3')
        }
      } catch {
        setPollError(true)
      }
    }

    void poll()
    intervalRef.current = setInterval(() => void poll(), POLL_INTERVAL_MS)

    return () => {
      if (intervalRef.current) clearInterval(intervalRef.current)
    }
  }, [step, tenantId, router])

  const progressPercent =
    progress && progress.total > 0
      ? Math.round((progress.synced / progress.total) * 100)
      : 0

  return (
    <div className="mx-auto max-w-lg">
      {/* Header */}
      <div className="mb-6">
        <div className="flex items-center gap-3 mb-2">
          <div className="flex h-10 w-10 items-center justify-center rounded-xl bg-orange-500 text-white">
            <ShoppingBag size={20} />
          </div>
          <div>
            <h1 className="text-2xl font-bold text-slate-900">เชื่อมต่อ Shopee</h1>
            <p className="text-sm text-slate-500">ดึงข้อมูลออเดอร์และคำนวณ margin อัตโนมัติ</p>
          </div>
        </div>

        {/* Step indicator */}
        <div className="mt-4 flex items-center gap-2">
          {[1, 2, 3].map((s) => (
            <div key={s} className="flex items-center gap-2">
              <div
                className={`flex h-7 w-7 items-center justify-center rounded-full text-xs font-semibold ${
                  step > s
                    ? 'bg-indigo-600 text-white'
                    : step === s
                      ? 'border-2 border-indigo-600 bg-white text-indigo-600'
                      : 'bg-slate-100 text-slate-400'
                }`}
              >
                {step > s ? <CheckCircle size={14} /> : s}
              </div>
              <span
                className={`text-xs ${step === s ? 'font-medium text-slate-700' : 'text-slate-400'}`}
              >
                {s === 1 ? 'เชื่อมต่อ' : s === 2 ? 'Sync ข้อมูล' : 'ตั้งค่า COGS'}
              </span>
              {s < 3 && <div className="h-px w-6 bg-slate-200" />}
            </div>
          ))}
        </div>
      </div>

      {/* Error state: OAuth failed */}
      {hasOAuthError && (
        <div className="mb-4 flex items-start gap-3 rounded-xl border border-red-200 bg-red-50 p-4">
          <AlertCircle size={18} className="mt-0.5 flex-shrink-0 text-red-500" />
          <div>
            <p className="text-sm font-semibold text-red-800">การเชื่อมต่อล้มเหลว</p>
            <p className="mt-0.5 text-xs text-red-600">
              ไม่สามารถเชื่อมต่อกับ Shopee ได้ กรุณาลองใหม่อีกครั้ง
            </p>
          </div>
        </div>
      )}

      {/* Step 1: Connect */}
      {step === 1 && (
        <div className="rounded-xl border border-slate-200 bg-white p-6 shadow-sm">
          <h2 className="mb-1 text-lg font-semibold text-slate-800">อนุญาตการเข้าถึง</h2>
          <p className="mb-6 text-sm text-slate-500">
            กดปุ่มด้านล่างเพื่อเข้าสู่ระบบ Shopee และอนุญาตให้ Ryla
            ดึงข้อมูลออเดอร์ของคุณ
          </p>
          <a
            href={authorizeUrl}
            className="inline-flex w-full items-center justify-center gap-2 rounded-lg bg-orange-500 px-4 py-3 text-sm font-semibold text-white hover:bg-orange-600 transition-colors"
          >
            <ShoppingBag size={16} />
            เชื่อมต่อกับ Shopee
            <ArrowRight size={16} />
          </a>
          <p className="mt-4 text-center text-xs text-slate-400">
            คุณจะถูก redirect ไปยัง Shopee เพื่อยืนยันสิทธิ์การเข้าถึง
          </p>
        </div>
      )}

      {/* Step 2: Sync progress */}
      {step === 2 && (
        <div className="rounded-xl border border-slate-200 bg-white p-6 shadow-sm">
          <div className="mb-4 flex items-center gap-3">
            {pollError ? (
              <AlertCircle size={20} className="flex-shrink-0 text-red-500" />
            ) : (
              <Loader2 size={20} className="flex-shrink-0 animate-spin text-indigo-600" />
            )}
            <h2 className="text-lg font-semibold text-slate-800">
              {pollError ? 'เกิดข้อผิดพลาด' : 'กำลัง Sync ออเดอร์...'}
            </h2>
          </div>

          {pollError ? (
            <div className="rounded-lg bg-red-50 p-3 text-sm text-red-700">
              ไม่สามารถตรวจสอบสถานะได้ กรุณาลองรีเฟรชหน้านี้
            </div>
          ) : (
            <>
              <div className="mb-2 flex justify-between text-xs text-slate-500">
                <span>
                  {progress
                    ? `${progress.synced} / ${progress.total} ออเดอร์`
                    : 'กำลังเชื่อมต่อ...'}
                </span>
                <span>{progressPercent}%</span>
              </div>
              <div className="h-2 w-full overflow-hidden rounded-full bg-slate-100">
                <div
                  className="h-full rounded-full bg-indigo-600 transition-all duration-500"
                  style={{ width: `${progressPercent}%` }}
                />
              </div>
              <p className="mt-4 text-xs text-slate-400">
                อาจใช้เวลาสักครู่ขึ้นอยู่กับจำนวนออเดอร์ — หน้านี้จะอัปเดตอัตโนมัติ
              </p>
            </>
          )}
        </div>
      )}

      {/* Step 3: COGS prompt */}
      {step === 3 && (
        <div className="rounded-xl border border-slate-200 bg-white p-6 shadow-sm">
          <div className="mb-4 flex items-center gap-2 text-emerald-600">
            <CheckCircle size={20} />
            <h2 className="text-lg font-semibold text-slate-800">Sync เสร็จแล้ว!</h2>
          </div>
          <p className="mb-2 text-sm text-slate-600">
            ออเดอร์ Shopee ถูกดึงมาเรียบร้อยแล้ว ขั้นตอนสุดท้ายคือกรอก
            ต้นทุนสินค้า (COGS) เพื่อให้ระบบคำนวณ net margin ได้อย่างแม่นยำ
          </p>
          <div className="mt-2 rounded-lg bg-amber-50 p-3 text-xs text-amber-800">
            ถ้าไม่กรอก COGS ระบบจะยังคำนวณ gross margin ให้ได้ แต่ net margin
            จะแสดงเป็น &ldquo;—&rdquo;
          </div>
          <div className="mt-6 flex flex-col gap-3 sm:flex-row">
            <Link
              href="/profit/sku-costs"
              className="inline-flex flex-1 items-center justify-center gap-2 rounded-lg bg-indigo-600 px-4 py-2.5 text-sm font-semibold text-white hover:bg-indigo-700 transition-colors"
            >
              กรอก cost เพื่อดู net margin
              <ArrowRight size={15} />
            </Link>
            <Link
              href="/profit"
              className="inline-flex flex-1 items-center justify-center rounded-lg border border-slate-200 px-4 py-2.5 text-sm font-medium text-slate-600 hover:bg-slate-50 transition-colors"
            >
              ดู Profit Dashboard
            </Link>
          </div>
        </div>
      )}
    </div>
  )
}
