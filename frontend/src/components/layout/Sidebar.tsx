'use client'

import Link from 'next/link'
import { usePathname, useRouter } from 'next/navigation'
import { createClient } from '@/lib/supabase/client'

const navItems = [
  { href: '/connections', label: 'Connections', icon: '⚙️' },
  { href: '/webhook', label: 'Webhook URL', icon: '⚡' },
]

export function Sidebar({ userName }: { userName: string }) {
  const pathname = usePathname()
  const router = useRouter()

  const handleSignOut = async () => {
    const supabase = createClient()
    await supabase.auth.signOut()
    router.push('/login')
  }

  return (
    <aside className="flex w-44 flex-shrink-0 flex-col bg-slate-900 text-slate-200">
      <div className="border-b border-slate-800 px-4 py-4">
        <span className="text-lg font-bold text-indigo-400">Ryla</span>
      </div>
      <nav className="flex-1 py-2">
        {navItems.map(({ href, label, icon }) => (
          <Link
            key={href}
            href={href}
            className={`mx-2 my-0.5 flex items-center gap-2 rounded-md px-3 py-2 text-sm transition-colors ${
              pathname.startsWith(href)
                ? 'bg-indigo-900 font-semibold text-indigo-200'
                : 'text-slate-400 hover:text-slate-200'
            }`}
          >
            <span>{icon}</span>
            {label}
          </Link>
        ))}
      </nav>
      <div className="border-t border-slate-800 px-4 py-3">
        <div className="flex items-center gap-2">
          <div className="flex h-6 w-6 flex-shrink-0 items-center justify-center rounded-full bg-slate-700 text-xs font-bold text-white">
            {userName.charAt(0).toUpperCase()}
          </div>
          <span className="flex-1 truncate text-xs text-slate-400">{userName}</span>
          <button
            onClick={handleSignOut}
            className="text-xs text-slate-500 hover:text-slate-300"
            title="Sign out"
          >
            ↩
          </button>
        </div>
      </div>
    </aside>
  )
}
