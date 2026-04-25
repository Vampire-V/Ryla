import { NextResponse } from 'next/server'
import { createClient } from '@/lib/supabase/server'

export async function POST() {
  const supabase = await createClient()

  const {
    data: { user },
  } = await supabase.auth.getUser()

  if (!user) {
    return NextResponse.json(
      { success: false, message: 'Unauthorized' },
      { status: 401 },
    )
  }

  const internalSecret = process.env['INTERNAL_API_SECRET']
  if (!internalSecret) {
    return NextResponse.json(
      { success: false, message: 'Internal error' },
      { status: 500 },
    )
  }

  const { data: profile, error: profileError } = await supabase
    .from('profiles')
    .select('tenant_id')
    .eq('id', user.id)
    .single()

  if (profileError && profileError.code !== 'PGRST116') {
    return NextResponse.json(
      { success: false, message: 'Internal error' },
      { status: 500 },
    )
  }
  if (!profile) {
    return NextResponse.json(
      { success: false, message: 'Profile not found' },
      { status: 404 },
    )
  }

  const backendUrl = process.env['NEXT_PUBLIC_BACKEND_URL'] ?? 'http://localhost:5282'

  try {
    const res = await fetch(`${backendUrl}/api/test-notification`, {
      method: 'POST',
      signal: AbortSignal.timeout(5000),
      headers: {
        'Content-Type': 'application/json',
        'X-Internal-Secret': internalSecret,
      },
      body: JSON.stringify({ tenantId: profile.tenant_id }),
    })

    const json = (await res.json()) as { success: boolean; message: string }
    return NextResponse.json(json, { status: res.ok ? 200 : res.status })
  } catch {
    return NextResponse.json(
      { success: false, message: 'Backend unreachable' },
      { status: 502 },
    )
  }
}
