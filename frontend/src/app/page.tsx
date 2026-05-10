import { MessageSquare, Sheet, Link, BellRing, TableProperties, Smartphone, ShieldCheck, ArrowRight } from 'lucide-react'
import { LandingMockup } from '@/components/landing/LandingMockup'
import { LandingScrollFade } from '@/components/landing/LandingScrollFade'

export default function HomePage() {
  return (
    <>
      <style>{`
        @keyframes fadeUp {
          from { opacity: 0; transform: translateY(24px); }
          to   { opacity: 1; transform: translateY(0); }
        }
        @keyframes fadeRight {
          from { opacity: 0; transform: translateX(32px); }
          to   { opacity: 1; transform: translateX(0); }
        }
        @keyframes pulse-dot {
          0%, 100% { opacity: 1; }
          50%       { opacity: 0.4; }
        }
        @keyframes typing-bounce {
          0%, 60%, 100% { transform: translateY(0); }
          30%            { transform: translateY(-6px); }
        }
        .anim-badge  { animation: fadeUp 0.5s ease both; animation-delay: 0.1s; }
        .anim-h1     { animation: fadeUp 0.5s ease both; animation-delay: 0.2s; }
        .anim-sub    { animation: fadeUp 0.5s ease both; animation-delay: 0.3s; }
        .anim-cta    { animation: fadeUp 0.5s ease both; animation-delay: 0.4s; }
        .anim-card   { animation: fadeRight 0.6s ease both; animation-delay: 0.5s; }
        .pulse-dot   { animation: pulse-dot 1.5s ease-in-out infinite; }
        .typing-dot  { display: inline-block; animation: typing-bounce 1.2s ease-in-out infinite; }
        .typing-dot:nth-child(2) { animation-delay: 0.2s; }
        .typing-dot:nth-child(3) { animation-delay: 0.4s; }
        .scroll-fade { opacity: 0; transform: translateY(20px); transition: opacity 0.5s ease, transform 0.5s ease; }
        .scroll-fade.visible { opacity: 1; transform: translateY(0); }
      `}</style>

      <LandingScrollFade />

      {/* SECTION 1: Hero */}
      <section
        className="relative min-h-screen overflow-hidden px-6 py-20 md:px-12"
        style={{ background: '#0F1117' }}
      >
        {/* Glow */}
        <div
          className="pointer-events-none absolute -left-32 -top-32 h-[500px] w-[500px] rounded-full opacity-20"
          style={{ background: 'radial-gradient(circle, #4F46E5 0%, transparent 70%)' }}
        />

        <div className="relative mx-auto grid max-w-6xl items-center gap-12 md:grid-cols-2">
          {/* Left */}
          <div>
            <span className="anim-badge mb-6 inline-flex items-center gap-2 rounded-full border border-indigo-500/30 bg-indigo-500/10 px-4 py-1.5 text-sm text-indigo-300">
              <span className="pulse-dot h-2 w-2 rounded-full bg-indigo-400" />
              ทดลองใช้ฟรี — ไม่ต้องใส่บัตรเครดิต
            </span>

            <h1 className="anim-h1 mt-4 text-5xl font-bold leading-tight tracking-tight text-white">
              ออเดอร์เข้า{' '}
              <span className="text-indigo-400">LINE</span>{' '}
              ทันที.<br />ทุกครั้ง.
            </h1>

            <p className="anim-sub mt-5 text-lg text-slate-400">
              เชื่อม TikTok Shop และ Shopee เข้ากับ LINE OA และ Google Sheets<br />
              รับแจ้งเตือนออเดอร์ใหม่โดยอัตโนมัติ — ไม่ต้องเขียนโค้ด
            </p>

            <div className="anim-cta mt-8 flex flex-col gap-3 sm:flex-row">
              <a
                href="/auth/signup"
                className="inline-flex items-center justify-center gap-2 rounded-lg bg-indigo-600 px-6 py-3 text-sm font-semibold text-white transition-colors hover:bg-indigo-700"
              >
                เริ่มใช้งานฟรี <ArrowRight size={15} />
              </a>
              <a
                href="/auth/login"
                className="inline-flex items-center justify-center rounded-lg border border-slate-600 px-6 py-3 text-sm font-semibold text-slate-300 transition-colors hover:border-slate-400 hover:text-white"
              >
                เข้าสู่ระบบ
              </a>
            </div>
          </div>

          {/* Right — LINE notification mockup (client component for animation) */}
          <div className="anim-card flex justify-center md:justify-end">
            <LandingMockup />
          </div>
        </div>
      </section>

      {/* SECTION 2: How it works */}
      <section className="px-6 py-20 md:px-12" style={{ background: '#F8FAFC' }}>
        <div className="mx-auto max-w-6xl">
          <h2 className="scroll-fade mb-12 text-center text-3xl font-bold text-slate-900">
            เริ่มต้นใน 3 ขั้นตอน
          </h2>
          <div className="grid gap-8 md:grid-cols-3">
            {(
              [
                { icon: MessageSquare, step: '1', title: 'เชื่อม LINE OA',           desc: 'วาง Channel Access Token จาก LINE Developers Console' },
                { icon: Sheet,         step: '2', title: 'เชื่อม Google Sheets',      desc: 'แชร์ Spreadsheet ให้ Service Account ที่ดาวน์โหลดจาก Google Cloud' },
                { icon: Link,          step: '3', title: 'คัดลอก Webhook URL',        desc: 'นำ URL ไปใส่ใน TikTok Shop หรือ Shopee — เสร็จแล้ว' },
              ] as const
            ).map(({ icon: Icon, step, title, desc }) => (
              <div key={step} className="scroll-fade rounded-2xl bg-white p-6 shadow-sm">
                <div className="mb-4 flex items-center gap-3">
                  <div className="flex h-10 w-10 items-center justify-center rounded-full bg-indigo-600 text-sm font-bold text-white">
                    {step}
                  </div>
                  <Icon size={20} className="text-indigo-500" />
                </div>
                <h3 className="mb-1 font-semibold text-slate-900">{title}</h3>
                <p className="text-sm text-slate-500">{desc}</p>
              </div>
            ))}
          </div>
        </div>
      </section>

      {/* SECTION 3: Features */}
      <section className="px-6 py-20 md:px-12" style={{ background: '#FFFFFF' }}>
        <div className="mx-auto max-w-6xl">
          <h2 className="scroll-fade mb-12 text-center text-3xl font-bold text-slate-900">
            ทำไมต้อง Ryla
          </h2>
          <div className="grid gap-6 sm:grid-cols-2">
            {(
              [
                { icon: BellRing,        title: 'แจ้งเตือน LINE ทันที',     desc: 'รู้ทุกออเดอร์ใหม่ผ่าน LINE OA ภายในวินาที — ไม่พลาดแม้แต่ออเดอร์เดียว' },
                { icon: TableProperties, title: 'บันทึก Sheets อัตโนมัติ',  desc: 'ข้อมูลออเดอร์ append ลง Google Sheets ทันที ไม่ต้องกรอกมือ ไม่ผิดพลาด' },
                { icon: Smartphone,      title: 'รองรับหลาย platform',       desc: 'เชื่อมต่อ TikTok Shop และ Shopee พร้อมกันจากที่เดียว ขยายได้ง่าย' },
                { icon: ShieldCheck,     title: 'ปลอดภัย มั่นใจได้',         desc: 'ข้อมูลเข้ารหัส ไม่มีการเก็บรหัสผ่าน รองรับ Row Level Security ทุกชั้น' },
              ] as const
            ).map(({ icon: Icon, title, desc }) => (
              <div key={title} className="scroll-fade rounded-2xl border border-slate-100 p-6 shadow-sm">
                <div className="mb-4 flex h-11 w-11 items-center justify-center rounded-xl bg-indigo-50">
                  <Icon size={22} className="text-indigo-600" />
                </div>
                <h3 className="mb-1 font-semibold text-slate-900">{title}</h3>
                <p className="text-sm text-slate-500">{desc}</p>
              </div>
            ))}
          </div>
        </div>
      </section>

      {/* SECTION 4: CTA */}
      <section className="px-6 py-20 md:px-12" style={{ background: '#4F46E5' }}>
        <div className="mx-auto max-w-2xl text-center">
          <h2 className="scroll-fade mb-4 text-3xl font-bold text-white">
            พร้อมเริ่มต้นแล้วหรือยัง?
          </h2>
          <p className="scroll-fade mb-8 text-indigo-200">
            เริ่มต้นฟรี ไม่ต้องใส่ข้อมูลบัตรเครดิต
          </p>
          <a
            href="/auth/signup"
            className="scroll-fade inline-flex items-center gap-2 rounded-lg bg-white px-8 py-3 text-sm font-semibold text-indigo-700 transition-colors hover:bg-indigo-50"
          >
            เริ่มใช้งานฟรี <ArrowRight size={15} />
          </a>
        </div>
      </section>
    </>
  )
}
