import { notFound } from 'next/navigation'
import { createClient } from '@/lib/supabase/server'
import { LineForm } from '@/components/connections/LineForm'
import { SheetsForm } from '@/components/connections/SheetsForm'
import type { LineCredentials, GoogleSheetsCredentials } from '@/lib/connections'

type Props = {
  params: Promise<{ platform: string }>
}

export default async function NewConnectionPage({ params }: Props) {
  const { platform } = await params
  const supabase = await createClient()

  if (platform === 'line') {
    const { data: conn } = await supabase
      .from('connections')
      .select('credentials')
      .eq('platform', 'line_oa')
      .maybeSingle()

    const creds = conn?.credentials as LineCredentials | null
    return (
      <div>
        <h1 className="mb-6 text-lg font-semibold text-slate-900">
          {creds ? 'แก้ไข LINE OA' : 'เพิ่ม LINE OA'}
        </h1>
        <LineForm
          initialToken={creds?.channel_access_token ?? ''}
          initialUserId={creds?.target_user_id ?? ''}
        />
      </div>
    )
  }

  if (platform === 'google-sheets') {
    const { data: conn } = await supabase
      .from('connections')
      .select('credentials')
      .eq('platform', 'google_sheets')
      .maybeSingle()

    const creds = conn?.credentials as GoogleSheetsCredentials | null
    return (
      <div>
        <h1 className="mb-6 text-lg font-semibold text-slate-900">
          {creds ? 'แก้ไข Google Sheets' : 'เพิ่ม Google Sheets'}
        </h1>
        <SheetsForm
          initialSpreadsheetId={creds?.spreadsheet_id ?? ''}
          initialWorksheetName={creds?.worksheet_name ?? 'Orders'}
        />
      </div>
    )
  }

  notFound()
}
