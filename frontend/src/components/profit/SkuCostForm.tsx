'use client'

// "use client" จำเป็น: form state, inline edit, POST API call

import { useState } from 'react'
import { Pencil, Check, X, Plus, Loader2 } from 'lucide-react'
import type { SkuCost } from '@/types/profit'

interface SkuCostFormProps {
  items: SkuCost[]
  tenantId: string
}

interface EditState {
  itemSku: string
  itemName: string
  cogs: string
}

interface FormError {
  message: string
}

export function SkuCostTable({ items: initialItems, tenantId }: SkuCostFormProps) {
  const [items, setItems] = useState<SkuCost[]>(initialItems)
  const [editingSku, setEditingSku] = useState<string | null>(null)
  const [editState, setEditState] = useState<EditState>({ itemSku: '', itemName: '', cogs: '' })
  const [isSubmitting, setIsSubmitting] = useState(false)
  const [error, setError] = useState<FormError | null>(null)

  // สำหรับเพิ่มรายการใหม่
  const [showAdd, setShowAdd] = useState(false)
  const [addState, setAddState] = useState<EditState>({ itemSku: '', itemName: '', cogs: '' })

  const apiUrl = process.env['NEXT_PUBLIC_API_URL'] ?? ''

  const startEdit = (item: SkuCost) => {
    setEditingSku(item.itemSku)
    setEditState({
      itemSku: item.itemSku,
      itemName: item.itemName ?? '',
      cogs: String(item.cogs),
    })
    setError(null)
  }

  const cancelEdit = () => {
    setEditingSku(null)
    setError(null)
  }

  const validateCogs = (value: string): number | null => {
    const n = parseFloat(value)
    if (isNaN(n) || n <= 0) return null
    return n
  }

  const submitCost = async (state: EditState): Promise<SkuCost | null> => {
    const cogs = validateCogs(state.cogs)
    if (!cogs) {
      setError({ message: 'COGS ต้องเป็นตัวเลขที่มากกว่า 0' })
      return null
    }
    if (!state.itemSku.trim()) {
      setError({ message: 'กรุณากรอก Item SKU' })
      return null
    }

    setIsSubmitting(true)
    setError(null)

    try {
      const res = await fetch(`${apiUrl}/api/profit/sku-costs`, {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
          'X-Tenant-Id': tenantId,
        },
        body: JSON.stringify({
          itemSku: state.itemSku.trim(),
          itemName: state.itemName.trim() || null,
          cogs,
        }),
      })

      if (!res.ok) {
        const text = await res.text()
        setError({ message: `บันทึกไม่สำเร็จ: ${text || res.statusText}` })
        return null
      }

      return (await res.json()) as SkuCost
    } catch {
      setError({ message: 'ไม่สามารถเชื่อมต่อ API ได้ กรุณาลองใหม่' })
      return null
    } finally {
      setIsSubmitting(false)
    }
  }

  const handleSaveEdit = async () => {
    const saved = await submitCost(editState)
    if (!saved) return

    setItems((prev) =>
      prev.map((item) => (item.itemSku === editState.itemSku ? saved : item)),
    )
    setEditingSku(null)
  }

  const handleAddNew = async () => {
    const saved = await submitCost(addState)
    if (!saved) return

    // ถ้ามี SKU นี้อยู่แล้วให้ update แทน add ซ้ำ
    setItems((prev) => {
      const exists = prev.some((item) => item.itemSku === saved.itemSku)
      if (exists) return prev.map((item) => (item.itemSku === saved.itemSku ? saved : item))
      return [...prev, saved]
    })
    setAddState({ itemSku: '', itemName: '', cogs: '' })
    setShowAdd(false)
  }

  return (
    <div>
      {/* Error banner */}
      {error && (
        <div className="mb-4 flex items-start gap-3 rounded-lg border border-red-200 bg-red-50 p-3 text-sm text-red-700">
          <X size={15} className="mt-0.5 flex-shrink-0" />
          {error.message}
        </div>
      )}

      <div className="rounded-xl border border-slate-200 bg-white shadow-sm overflow-hidden">
        <table className="min-w-full">
          <thead>
            <tr className="border-b border-slate-200 bg-slate-50">
              <th className="py-3 pl-4 pr-3 text-left text-xs font-semibold uppercase tracking-wide text-slate-400">
                Item SKU
              </th>
              <th className="px-3 py-3 text-left text-xs font-semibold uppercase tracking-wide text-slate-400">
                ชื่อสินค้า
              </th>
              <th className="px-3 py-3 text-right text-xs font-semibold uppercase tracking-wide text-slate-400">
                COGS (บาท)
              </th>
              <th className="w-24 px-3 py-3 text-right text-xs font-semibold uppercase tracking-wide text-slate-400">
                แก้ไข
              </th>
            </tr>
          </thead>
          <tbody>
            {items.map((item) =>
              editingSku === item.itemSku ? (
                <tr key={item.itemSku} className="border-b border-slate-100 bg-indigo-50/40">
                  <td className="py-2 pl-4 pr-3">
                    <span className="text-xs font-mono text-slate-600">{item.itemSku}</span>
                  </td>
                  <td className="px-3 py-2">
                    <input
                      type="text"
                      value={editState.itemName}
                      onChange={(e) => setEditState((s) => ({ ...s, itemName: e.target.value }))}
                      placeholder="ชื่อสินค้า (ไม่บังคับ)"
                      className="w-full rounded-md border border-slate-300 px-2 py-1 text-sm text-slate-700 focus:border-indigo-400 focus:outline-none focus:ring-1 focus:ring-indigo-400"
                    />
                  </td>
                  <td className="px-3 py-2">
                    <input
                      type="number"
                      min="0.01"
                      step="0.01"
                      value={editState.cogs}
                      onChange={(e) => setEditState((s) => ({ ...s, cogs: e.target.value }))}
                      placeholder="0.00"
                      className="w-full rounded-md border border-slate-300 px-2 py-1 text-right text-sm text-slate-700 focus:border-indigo-400 focus:outline-none focus:ring-1 focus:ring-indigo-400"
                    />
                  </td>
                  <td className="px-3 py-2 pr-4">
                    <div className="flex justify-end gap-1">
                      <button
                        type="button"
                        onClick={() => void handleSaveEdit()}
                        disabled={isSubmitting}
                        className="flex items-center gap-1 rounded-md bg-indigo-600 px-2 py-1 text-xs font-medium text-white hover:bg-indigo-700 disabled:opacity-50 transition-colors"
                      >
                        {isSubmitting ? <Loader2 size={12} className="animate-spin" /> : <Check size={12} />}
                        บันทึก
                      </button>
                      <button
                        type="button"
                        onClick={cancelEdit}
                        disabled={isSubmitting}
                        className="rounded-md border border-slate-200 px-2 py-1 text-xs text-slate-500 hover:bg-slate-50 disabled:opacity-50 transition-colors"
                      >
                        <X size={12} />
                      </button>
                    </div>
                  </td>
                </tr>
              ) : (
                <tr
                  key={item.itemSku}
                  className="border-b border-slate-100 hover:bg-slate-50/50 transition-colors"
                >
                  <td className="py-3 pl-4 pr-3 text-xs font-mono text-slate-700">
                    {item.itemSku}
                  </td>
                  <td className="px-3 py-3 text-sm text-slate-600">
                    {item.itemName ?? <span className="text-slate-300">—</span>}
                  </td>
                  <td className="px-3 py-3 text-right text-sm font-medium text-slate-700">
                    {new Intl.NumberFormat('th-TH', {
                      style: 'currency',
                      currency: 'THB',
                    }).format(item.cogs)}
                  </td>
                  <td className="px-3 py-3 pr-4 text-right">
                    <button
                      type="button"
                      onClick={() => startEdit(item)}
                      className="inline-flex items-center gap-1 rounded-md px-2 py-1 text-xs text-slate-400 hover:bg-slate-100 hover:text-slate-600 transition-colors"
                    >
                      <Pencil size={12} />
                      แก้ไข
                    </button>
                  </td>
                </tr>
              ),
            )}

            {/* Add new row */}
            {showAdd && (
              <tr className="border-b border-slate-100 bg-emerald-50/40">
                <td className="py-2 pl-4 pr-3">
                  <input
                    type="text"
                    value={addState.itemSku}
                    onChange={(e) => setAddState((s) => ({ ...s, itemSku: e.target.value }))}
                    placeholder="SKU-001"
                    className="w-full rounded-md border border-slate-300 px-2 py-1 text-xs font-mono text-slate-700 focus:border-indigo-400 focus:outline-none focus:ring-1 focus:ring-indigo-400"
                  />
                </td>
                <td className="px-3 py-2">
                  <input
                    type="text"
                    value={addState.itemName}
                    onChange={(e) => setAddState((s) => ({ ...s, itemName: e.target.value }))}
                    placeholder="ชื่อสินค้า (ไม่บังคับ)"
                    className="w-full rounded-md border border-slate-300 px-2 py-1 text-sm text-slate-700 focus:border-indigo-400 focus:outline-none focus:ring-1 focus:ring-indigo-400"
                  />
                </td>
                <td className="px-3 py-2">
                  <input
                    type="number"
                    min="0.01"
                    step="0.01"
                    value={addState.cogs}
                    onChange={(e) => setAddState((s) => ({ ...s, cogs: e.target.value }))}
                    placeholder="0.00"
                    className="w-full rounded-md border border-slate-300 px-2 py-1 text-right text-sm text-slate-700 focus:border-indigo-400 focus:outline-none focus:ring-1 focus:ring-indigo-400"
                  />
                </td>
                <td className="px-3 py-2 pr-4">
                  <div className="flex justify-end gap-1">
                    <button
                      type="button"
                      onClick={() => void handleAddNew()}
                      disabled={isSubmitting}
                      className="flex items-center gap-1 rounded-md bg-emerald-600 px-2 py-1 text-xs font-medium text-white hover:bg-emerald-700 disabled:opacity-50 transition-colors"
                    >
                      {isSubmitting ? <Loader2 size={12} className="animate-spin" /> : <Plus size={12} />}
                      เพิ่ม
                    </button>
                    <button
                      type="button"
                      onClick={() => { setShowAdd(false); setError(null) }}
                      disabled={isSubmitting}
                      className="rounded-md border border-slate-200 px-2 py-1 text-xs text-slate-500 hover:bg-slate-50 disabled:opacity-50 transition-colors"
                    >
                      <X size={12} />
                    </button>
                  </div>
                </td>
              </tr>
            )}
          </tbody>
        </table>
      </div>

      {/* Add button */}
      {!showAdd && (
        <button
          type="button"
          onClick={() => { setShowAdd(true); setError(null) }}
          className="mt-3 inline-flex items-center gap-2 rounded-lg border border-dashed border-slate-300 bg-white px-4 py-2 text-sm font-medium text-slate-500 hover:border-slate-400 hover:text-slate-700 transition-colors"
        >
          <Plus size={15} />
          เพิ่ม SKU ใหม่
        </button>
      )}
    </div>
  )
}
