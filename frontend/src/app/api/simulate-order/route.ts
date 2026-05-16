import { NextResponse } from 'next/server'
import { createClient } from '@/lib/supabase/server'
import type { SimulatorPlatform } from '@/lib/simulator/platforms'

export async function POST(request: Request) {
  const supabase = await createClient()

  const {
    data: { user },
  } = await supabase.auth.getUser()

  if (!user) {
    return NextResponse.json({ lineSuccess: false, sheetsSuccess: false, error: 'Unauthorized' }, { status: 401 })
  }

  const internalSecret = process.env['INTERNAL_API_SECRET']
  if (!internalSecret) {
    return NextResponse.json({ lineSuccess: false, sheetsSuccess: false, error: 'Internal error' }, { status: 500 })
  }

  const { data: profile, error: profileError } = await supabase
    .from('profiles')
    .select('tenant_id')
    .eq('id', user.id)
    .single()

  if (profileError && profileError.code !== 'PGRST116') {
    return NextResponse.json({ lineSuccess: false, sheetsSuccess: false, error: 'Internal error' }, { status: 500 })
  }
  if (!profile) {
    return NextResponse.json({ lineSuccess: false, sheetsSuccess: false, error: 'Profile not found' }, { status: 404 })
  }

  const backendUrl = process.env['NEXT_PUBLIC_BACKEND_URL'] ?? 'http://localhost:5282'

  try {
    const body = (await request.json()) as { platform: SimulatorPlatform }
    const res = await fetch(`${backendUrl}/api/simulate-order`, {
      method: 'POST',
      signal: AbortSignal.timeout(10000),
      headers: {
        'Content-Type': 'application/json',
        'X-Internal-Secret': internalSecret,
      },
      body: JSON.stringify({ tenantId: profile.tenant_id, platform: body.platform }),
    })

    const json = await res.json() as {
      lineSuccess: boolean
      sheetsSuccess: boolean
      orderId: string
      lineError: string | null
      sheetsError: string | null
    }
    return NextResponse.json(json, { status: res.ok ? 200 : res.status })
  } catch {
    return NextResponse.json(
      { lineSuccess: false, sheetsSuccess: false, error: 'Backend unreachable' },
      { status: 502 },
    )
  }
}
