'use client';

import { createBrowserClient } from '@supabase/ssr';
import type { Database } from '@/types/database.types';

// Browser-side Supabase client สำหรับ Client Components
// Database generic ทำให้ query มี type safety เต็มรูปแบบ
export const createClient = () =>
  createBrowserClient<Database>(
    process.env['NEXT_PUBLIC_SUPABASE_URL']!,
    process.env['NEXT_PUBLIC_SUPABASE_ANON_KEY']!,
  );
