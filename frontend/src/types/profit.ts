export interface ProfitSummary {
  totalRevenue: number
  totalGrossMargin: number
  totalNetMargin: number | null
  grossMarginPercent: number | null
  netMarginPercent: number | null
  completedOrderCount: number
  pendingSettlementCount: number
}

export interface ShopeeOrder {
  id: string
  orderSn: string
  status: string
  syncStatus: string
  revenue: number | null
  grossMargin: number | null
  netMargin: number | null
  orderCreatedAt: string | null
  syncedAt: string | null
}

export interface OrdersResponse {
  orders: ShopeeOrder[]
  totalCount: number
  page: number
  pageSize: number
}

export interface SkuCost {
  id: string
  itemSku: string
  itemName: string | null
  cogs: number
}

export interface SkuCostsResponse {
  items: SkuCost[]
}

export interface SyncProgress {
  synced: number
  pending: number
  total: number
}
