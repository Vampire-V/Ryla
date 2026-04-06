export default function HomePage() {
  return (
    <main className="flex min-h-screen flex-col items-center justify-center bg-white px-6 dark:bg-zinc-950">
      <div className="max-w-2xl text-center">
        <h1 className="text-5xl font-bold tracking-tight text-zinc-900 dark:text-white">
          Ryla
        </h1>
        <p className="mt-4 text-xl text-zinc-600 dark:text-zinc-400">
          เชื่อมต่อ TikTok Shop และ Shopee เข้ากับ LINE OA และ Google Sheets
          โดยอัตโนมัติ
        </p>
        <p className="mt-2 text-sm text-zinc-400">
          Automation สำหรับ SME ไทย ในราคาที่จับต้องได้
        </p>
        <div className="mt-10 flex flex-col items-center gap-4 sm:flex-row sm:justify-center">
          <a
            href="/auth/signup"
            className="rounded-full bg-zinc-900 px-8 py-3 text-sm font-semibold text-white transition-colors hover:bg-zinc-700 dark:bg-white dark:text-zinc-900 dark:hover:bg-zinc-200"
          >
            เริ่มใช้งานฟรี
          </a>
          <a
            href="/auth/login"
            className="rounded-full border border-zinc-200 px-8 py-3 text-sm font-semibold text-zinc-900 transition-colors hover:border-zinc-400 dark:border-zinc-700 dark:text-white"
          >
            เข้าสู่ระบบ
          </a>
        </div>
      </div>
    </main>
  );
}
