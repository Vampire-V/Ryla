'use client'

import { useEffect, useState } from 'react'

export function LandingMockup() {
  const [showBubble1, setShowBubble1] = useState(false)
  const [showBubble2, setShowBubble2] = useState(false)
  const [showStrip, setShowStrip]     = useState(false)
  const [showTyping, setShowTyping]   = useState(true)
  const [orders, setOrders]           = useState(0)
  const [uptime, setUptime]           = useState(0)

  function animateCounter(
    setter: (v: number) => void,
    from: number,
    to: number,
    duration: number,
  ) {
    const start = performance.now()
    const step = (now: number) => {
      const progress = Math.min((now - start) / duration, 1)
      setter(Math.round(from + (to - from) * progress))
      if (progress < 1) requestAnimationFrame(step)
    }
    requestAnimationFrame(step)
  }

  useEffect(() => {
    const t1 = setTimeout(() => setShowTyping(false), 800)
    const t2 = setTimeout(() => setShowBubble1(true),  1800)
    const t3 = setTimeout(() => setShowBubble2(true),  2400)
    const t4 = setTimeout(() => {
      setShowStrip(true)
      animateCounter(setOrders, 0, 47, 800)
      animateCounter(setUptime, 0, 99, 800)
    }, 2600)
    return () => { clearTimeout(t1); clearTimeout(t2); clearTimeout(t3); clearTimeout(t4) }
  }, [])

  return (
    <div
      className="w-full max-w-sm rounded-2xl border border-slate-700/50 p-5 shadow-2xl"
      style={{ background: '#1A1F2E' }}
    >
      {/* Chat header */}
      <div className="mb-4 flex items-center gap-3 border-b border-slate-700/50 pb-4">
        <div className="flex h-9 w-9 items-center justify-center rounded-full bg-green-500 text-sm font-bold text-white">
          R
        </div>
        <div>
          <p className="text-sm font-semibold text-white">Ryla Notify</p>
          <div className="flex items-center gap-1.5">
            <span className="h-1.5 w-1.5 rounded-full bg-green-400" />
            <span className="text-xs text-slate-400">Online</span>
          </div>
        </div>
      </div>

      {/* Typing indicator */}
      {showTyping && (
        <div className="mb-3 flex gap-1 px-1">
          <span className="typing-dot h-2 w-2 rounded-full bg-slate-500" />
          <span className="typing-dot h-2 w-2 rounded-full bg-slate-500" />
          <span className="typing-dot h-2 w-2 rounded-full bg-slate-500" />
        </div>
      )}

      {/* Bubble 1 */}
      {showBubble1 && (
        <div className="mb-3 rounded-2xl rounded-tl-sm p-4 text-xs" style={{ background: '#2A3147' }}>
          <p className="mb-2 font-semibold text-green-400">ออเดอร์ใหม่ — TikTok Shop</p>
          <div className="space-y-1 text-slate-300">
            <p><span className="text-slate-500">Order ID:</span> TK-20260511-0847</p>
            <p><span className="text-slate-500">ลูกค้า:</span> คุณสมชาย ใจดี</p>
            <p><span className="text-slate-500">สินค้า:</span> เคสโทรศัพท์ × 2</p>
            <p><span className="text-slate-500">ยอด:</span> ฿ 349.00</p>
            <p className="mt-1.5">
              <span className="rounded-full bg-green-500/20 px-2 py-0.5 text-green-400">ชำระแล้ว</span>
            </p>
          </div>
        </div>
      )}

      {/* Bubble 2 */}
      {showBubble2 && (
        <div className="mb-4 rounded-2xl rounded-tl-sm px-4 py-3 text-xs text-slate-300" style={{ background: '#2A3147' }}>
          บันทึกลง Google Sheets เรียบร้อย
        </div>
      )}

      {/* Stat strip */}
      {showStrip && (
        <div className="rounded-xl p-3 text-center" style={{ background: '#0F1117' }}>
          <div className="flex divide-x divide-slate-700">
            <div className="flex-1 px-2">
              <p className="text-lg font-bold text-white">{orders}</p>
              <p className="text-xs text-slate-400">ออเดอร์วันนี้</p>
            </div>
            <div className="flex-1 px-2">
              <p className="text-lg font-bold text-white">&lt;&nbsp;1s</p>
              <p className="text-xs text-slate-400">latency</p>
            </div>
            <div className="flex-1 px-2">
              <p className="text-lg font-bold text-white">{uptime}%</p>
              <p className="text-xs text-slate-400">uptime</p>
            </div>
          </div>
        </div>
      )}
    </div>
  )
}
