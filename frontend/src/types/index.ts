// Global types สำหรับ Ryla
// Types ที่ใช้เฉพาะ feature ให้วางไว้ใน folder ของ feature นั้น

export type Platform = 'tiktok_shop' | 'shopee' | 'line_oa' | 'google_sheets';

export interface Tenant {
  id: string;
  name: string;
  createdAt: string;
}

export interface Connection {
  id: string;
  tenantId: string;
  platform: Platform;
  isActive: boolean;
  createdAt: string;
}
