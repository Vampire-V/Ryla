/**
 * Ryla Database Types
 *
 * Auto-generated จาก Supabase schema — ห้ามแก้ไขโดยตรง
 * Regenerate ด้วยคำสั่ง:
 *   make types
 *   หรือ: supabase gen types typescript --local > src/types/database.types.ts
 *
 * @see docs/local-dev.md
 */

export type Json =
  | string
  | number
  | boolean
  | null
  | { [key: string]: Json | undefined }
  | Json[];

export type Platform = 'tiktok_shop' | 'shopee' | 'line_oa' | 'google_sheets';

export interface Database {
  public: {
    Tables: {
      tenants: {
        Row: {
          id: string;
          name: string;
          created_at: string;
        };
        Insert: {
          id?: string;
          name: string;
          created_at?: string;
        };
        Update: {
          id?: string;
          name?: string;
          created_at?: string;
        };
      };
      profiles: {
        Row: {
          id: string;
          tenant_id: string;
          full_name: string | null;
          created_at: string;
        };
        Insert: {
          id: string;
          tenant_id: string;
          full_name?: string | null;
          created_at?: string;
        };
        Update: {
          id?: string;
          tenant_id?: string;
          full_name?: string | null;
          created_at?: string;
        };
      };
      connections: {
        Row: {
          id: string;
          tenant_id: string;
          platform: Platform;
          credentials: Json;
          is_active: boolean;
          created_at: string;
        };
        Insert: {
          id?: string;
          tenant_id: string;
          platform: Platform;
          credentials?: Json;
          is_active?: boolean;
          created_at?: string;
        };
        Update: {
          id?: string;
          tenant_id?: string;
          platform?: Platform;
          credentials?: Json;
          is_active?: boolean;
          created_at?: string;
        };
      };
    };
    Views: Record<string, never>;
    Functions: Record<string, never>;
    Enums: {
      platform: Platform;
    };
  };
}

// Convenience types
export type Tables<T extends keyof Database['public']['Tables']> =
  Database['public']['Tables'][T]['Row'];

export type InsertTables<T extends keyof Database['public']['Tables']> =
  Database['public']['Tables'][T]['Insert'];

export type UpdateTables<T extends keyof Database['public']['Tables']> =
  Database['public']['Tables'][T]['Update'];
