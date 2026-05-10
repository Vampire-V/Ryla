'use client'

import Link from 'next/link'
import { usePathname, useRouter } from 'next/navigation'
import { Settings2, Webhook, LogOut } from 'lucide-react'
import { createClient } from '@/lib/supabase/client'

const navItems = [
  { href: '/connections', label: 'Connections', Icon: Settings2 },
  { href: '/webhook', label: 'Webhook URL', Icon: Webhook },
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
    <aside className="flex w-56 flex-shrink-0 flex-col bg-[var(--color-sidebar)] text-slate-200">
      <div className="border-b border-[var(--color-sidebar-border)] px-4 py-4">
        <span className="text-xl font-bold text-white">Ryla</span>
      </div>
      <nav className="flex-1 py-2">
        {navItems.map(({ href, label, Icon }) => (
          <Link
            key={href}
            href={href}
            className={`mx-2 my-0.5 flex items-center gap-2 rounded-md px-3 py-2 text-sm transition-colors ${
              pathname.startsWith(href)
                ? 'border-l-2 border-indigo-500 bg-white/5 font-semibold text-white'
                : 'text-slate-400 hover:bg-white/5 hover:text-slate-200'
            }`}
          >
            <Icon size={16} />
            {label}
          </Link>
        ))}
      </nav>
      <div className="border-t border-[var(--color-sidebar-border)] px-4 py-3">
        <div className="flex items-center gap-2">
          <div className="flex h-6 w-6 flex-shrink-0 items-center justify-center rounded-full bg-indigo-600 text-xs font-bold text-white">
            {userName.charAt(0).toUpperCase()}
          </div>
          <span className="flex-1 truncate text-xs text-slate-300">{userName}</span>
          <button
            onClick={handleSignOut}
            className="text-slate-500 hover:text-slate-300"
            title="Sign out"
          >
            <LogOut size={14} />
          </button>
        </div>
      </div>
    </aside>
  )
}
