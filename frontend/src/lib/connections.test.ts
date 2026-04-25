import { describe, it, expect, vi, beforeEach } from 'vitest'
import {
  getTenantId,
  upsertLineConnection,
  upsertSheetsConnection,
  deleteConnection,
} from './connections'
import type { LineCredentials, GoogleSheetsCredentials } from './connections'

vi.mock('@/lib/supabase/client', () => ({
  createClient: () => mockSupabase,
}))

const mockSelect = vi.fn()
const mockEq = vi.fn()
const mockSingle = vi.fn()
const mockUpsert = vi.fn()
const mockDelete = vi.fn()
const mockDeleteEq = vi.fn()

const mockSupabase = {
  auth: { getUser: vi.fn() },
  from: vi.fn(),
}

beforeEach(() => {
  vi.clearAllMocks()
  mockSelect.mockReturnThis()
  mockEq.mockReturnThis()
})

describe('getTenantId', () => {
  it('returns tenant_id when user is authenticated', async () => {
    mockSupabase.auth.getUser.mockResolvedValue({
      data: { user: { id: 'user-abc' } },
      error: null,
    })
    mockSingle.mockResolvedValue({ data: { tenant_id: 'tenant-xyz' }, error: null })
    mockSupabase.from.mockReturnValue({
      select: mockSelect,
      eq: mockEq,
      single: mockSingle,
    })
    mockSelect.mockReturnValue({ eq: mockEq })
    mockEq.mockReturnValue({ single: mockSingle })

    const result = await getTenantId()

    expect(result).toBe('tenant-xyz')
    expect(mockSupabase.from).toHaveBeenCalledWith('profiles')
  })

  it('throws when user is null', async () => {
    mockSupabase.auth.getUser.mockResolvedValue({ data: { user: null }, error: null })

    await expect(getTenantId()).rejects.toThrow('Not authenticated')
  })

  it('throws when profile not found', async () => {
    mockSupabase.auth.getUser.mockResolvedValue({
      data: { user: { id: 'user-abc' } },
      error: null,
    })
    mockSingle.mockResolvedValue({ data: null, error: { message: 'not found' } })
    mockSupabase.from.mockReturnValue({
      select: vi.fn().mockReturnValue({ eq: vi.fn().mockReturnValue({ single: mockSingle }) }),
    })

    await expect(getTenantId()).rejects.toThrow('Profile not found')
  })
})

describe('upsertLineConnection', () => {
  it('calls upsert with line_oa platform and correct credentials', async () => {
    mockUpsert.mockResolvedValue({ error: null })
    mockSupabase.from.mockReturnValue({ upsert: mockUpsert })

    const creds: LineCredentials = {
      channel_access_token: 'tok-123',
      target_user_id: 'Uabc',
    }
    await upsertLineConnection('tenant-1', creds)

    expect(mockUpsert).toHaveBeenCalledWith(
      expect.objectContaining({
        tenant_id: 'tenant-1',
        platform: 'line_oa',
        credentials: creds,
        is_active: true,
      }),
      { onConflict: 'tenant_id,platform' },
    )
  })

  it('throws on Supabase error', async () => {
    mockSupabase.from.mockReturnValue({
      upsert: vi.fn().mockResolvedValue({ error: { message: 'unique violation' } }),
    })

    await expect(
      upsertLineConnection('t1', { channel_access_token: 'x', target_user_id: 'y' }),
    ).rejects.toMatchObject({ message: 'unique violation' })
  })
})

describe('upsertSheetsConnection', () => {
  it('calls upsert with google_sheets platform', async () => {
    mockUpsert.mockResolvedValue({ error: null })
    mockSupabase.from.mockReturnValue({ upsert: mockUpsert })

    const creds: GoogleSheetsCredentials = {
      spreadsheet_id: 'sheet-id',
      worksheet_name: 'Orders',
      service_account_json: '{"type":"service_account"}',
    }
    await upsertSheetsConnection('tenant-1', creds)

    expect(mockUpsert).toHaveBeenCalledWith(
      expect.objectContaining({ platform: 'google_sheets', credentials: creds }),
      { onConflict: 'tenant_id,platform' },
    )
  })
})

describe('deleteConnection', () => {
  it('calls delete().eq() with connection id', async () => {
    mockDeleteEq.mockResolvedValue({ error: null })
    mockDelete.mockReturnValue({ eq: mockDeleteEq })
    mockSupabase.from.mockReturnValue({ delete: mockDelete })

    await deleteConnection('conn-999')

    expect(mockDeleteEq).toHaveBeenCalledWith('id', 'conn-999')
  })
})
