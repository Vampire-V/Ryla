import { redirect } from 'next/navigation'
import { createClient } from '@/lib/supabase/server'
import { Sidebar } from '@/components/layout/Sidebar'

export default async function DashboardLayout({
  children,
}: {
  children: React.ReactNode
}) {
  const supabase = await createClient()
  const {
    data: { user },
  } = await supabase.auth.getUser()

  if (!user) redirect('/login')

  const { data: profile } = await supabase
    .from('profiles')
    .select('full_name')
    .eq('id', user.id)
    .single()

  const userName = profile?.full_name ?? user.email ?? 'User'

  return (
    <div className="flex h-screen overflow-hidden">
      <Sidebar userName={userName} />
      <main className="flex-1 overflow-auto bg-[var(--color-page-bg)] p-6">{children}</main>
    </div>
  )
}
