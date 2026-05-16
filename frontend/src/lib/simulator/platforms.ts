export const SIMULATOR_PLATFORMS = ['tiktok_shop'] as const
export type SimulatorPlatform = (typeof SIMULATOR_PLATFORMS)[number]

export const PLATFORM_LABELS: Record<SimulatorPlatform, string> = {
  tiktok_shop: 'TikTok Shop',
}
// เพิ่ม Shopee: เพิ่ม 'shopee' ใน SIMULATOR_PLATFORMS + label ที่นี่ที่เดียว
