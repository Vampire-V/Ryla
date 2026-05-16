import { redirect } from 'next/navigation'
import { createClient } from '@/lib/supabase/server'
import { ShopeeWizard } from '@/components/connections/ShopeeWizard'

// Server Component — ดึง tenantId แล้วส่งให้ ShopeeWizard (client)
export default async function ShopeeConnectionPage() {
  const supabase = await createClient()
  const {
    data: { user },
  } = await supabase.auth.getUser()

  if (!user) redirect('/login')

  const { data: profile } = await supabase
    .from('profiles')
    .select('tenant_id')
    .eq('id', user.id)
    .single()

  const tenantId = profile?.tenant_id
  if (!tenantId) redirect('/login')

  const apiUrl = process.env['NEXT_PUBLIC_API_URL'] ?? ''
  const authorizeUrl = `${apiUrl}/api/shopee/oauth/authorize?tenant_id=${tenantId}`

  return <ShopeeWizard tenantId={tenantId} authorizeUrl={authorizeUrl} />
}
