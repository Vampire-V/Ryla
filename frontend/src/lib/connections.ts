import { createClient } from '@/lib/supabase/client'
import type { Tables } from '@/types/database.types'

export type Connection = Tables<'connections'>

export type LineCredentials = {
  channel_access_token: string
  target_user_id: string
}

export type GoogleSheetsCredentials = {
  spreadsheet_id: string
  worksheet_name: string
  service_account_json: string
}

export async function getTenantId(): Promise<string> {
  const supabase = createClient()
  const {
    data: { user },
    error: userError,
  } = await supabase.auth.getUser()
  if (userError || !user) throw new Error('Not authenticated')

  const { data, error } = await supabase
    .from('profiles')
    .select('tenant_id')
    .eq('id', user.id)
    .single()

  if (error || !data) throw new Error('Profile not found')
  return data.tenant_id
}

export async function getConnections(): Promise<Connection[]> {
  const supabase = createClient()
  const { data, error } = await supabase
    .from('connections')
    .select('*')
    .order('created_at', { ascending: true })

  if (error) throw error
  return data ?? []
}

export async function upsertLineConnection(
  tenantId: string,
  credentials: LineCredentials,
): Promise<void> {
  const supabase = createClient()
  const { error } = await supabase
    .from('connections')
    .upsert(
      {
        tenant_id: tenantId,
        platform: 'line_oa',
        credentials: credentials as unknown as Record<string, string>,
        is_active: true,
      },
      { onConflict: 'tenant_id,platform' },
    )
  if (error) throw error
}

export async function upsertSheetsConnection(
  tenantId: string,
  credentials: GoogleSheetsCredentials,
): Promise<void> {
  const supabase = createClient()
  const { error } = await supabase
    .from('connections')
    .upsert(
      {
        tenant_id: tenantId,
        platform: 'google_sheets',
        credentials: credentials as unknown as Record<string, string>,
        is_active: true,
      },
      { onConflict: 'tenant_id,platform' },
    )
  if (error) throw error
}

export async function deleteConnection(id: string): Promise<void> {
  const supabase = createClient()
  const { error } = await supabase.from('connections').delete().eq('id', id)
  if (error) throw error
}
