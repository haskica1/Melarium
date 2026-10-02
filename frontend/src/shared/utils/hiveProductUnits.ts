import { HiveProductType } from '../../core/models'

// Display units of honey and the other bee products (SPEC-30). The server stores every quantity in kg
// and every price in KM/kg; this is the one place that decides what a beekeeper actually types and
// reads. Screens, the stats page and both report exports all go through it, so a product cannot read
// "350 g" on one and "0,35 kg" on another.
//
// Propolis, royal jelly and venom come in grams — a hive gives a few hundred grams of propolis, a
// few of royal jelly and milligrams of venom. Royal jelly and venom are also *sold* by the gram (a
// 10 g jar, a gram of dried venom), so their price is KM/g; raw propolis is traded per kg.

export type QtyUnit = 'kg' | 'g'

interface ProductUnits {
  qty: QtyUnit
  price: QtyUnit
  /** Most decimals worth showing in the display unit — venom is weighed to the milligram. */
  decimals: number
}

const UNITS: Record<HiveProductType, ProductUnits> = {
  [HiveProductType.Honey]:      { qty: 'kg', price: 'kg', decimals: 1 },
  [HiveProductType.CombHoney]:  { qty: 'kg', price: 'kg', decimals: 2 },
  [HiveProductType.Wax]:        { qty: 'kg', price: 'kg', decimals: 2 },
  [HiveProductType.Propolis]:   { qty: 'g',  price: 'kg', decimals: 1 },
  [HiveProductType.Pollen]:     { qty: 'kg', price: 'kg', decimals: 2 },
  [HiveProductType.RoyalJelly]: { qty: 'g',  price: 'g',  decimals: 1 },
  [HiveProductType.BeeBread]:   { qty: 'kg', price: 'kg', decimals: 2 },
  [HiveProductType.BeeVenom]:   { qty: 'g',  price: 'g',  decimals: 3 },
  [HiveProductType.Other]:      { qty: 'kg', price: 'kg', decimals: 2 },
}

/** Every type, in enum order — the order the server sorts by, so lists never reshuffle. */
export const HIVE_PRODUCT_TYPES: HiveProductType[] = Object.values(HiveProductType)
  .filter((v): v is HiveProductType => typeof v === 'number')

const perUnit = (unit: QtyUnit) => (unit === 'g' ? 1000 : 1)

export const qtyUnit = (type: HiveProductType): QtyUnit => UNITS[type].qty

/** "KM/kg" or "KM/g" — the unit the price field is entered in. */
export const priceUnitLabel = (type: HiveProductType) => `KM/${UNITS[type].price}`

/** kg (as stored) → the number shown in the product's unit. */
export const kgToDisplay = (kg: number, type: HiveProductType) => kg * perUnit(UNITS[type].qty)

/** A number typed in the product's unit → kg for the server. Rounded to the milligram the column holds. */
export const displayToKg = (value: number, type: HiveProductType) =>
  Math.round((value / perUnit(UNITS[type].qty)) * 1e6) / 1e6

/** KM/kg (as stored) → the price shown in the product's price unit. */
export const pricePerKgToDisplay = (pricePerKg: number, type: HiveProductType) =>
  pricePerKg / perUnit(UNITS[type].price)

/** A price typed per the product's price unit → KM/kg for the server. */
export const displayToPricePerKg = (price: number, type: HiveProductType) =>
  Math.round(price * perUnit(UNITS[type].price) * 100) / 100

/** "12,5 kg", "350 g", "1,234 g" — trailing zeros dropped, Bosnian separators. */
export function fmtProductQty(kg: number, type: HiveProductType): string {
  return `${fmtProductNumber(kg, type)} ${UNITS[type].qty}`
}

/** "12,5", "350", "1,234" — the number alone, for a table whose column header carries the unit. */
export function fmtProductNumber(kg: number, type: HiveProductType): string {
  return kgToDisplay(kg, type).toLocaleString('bs-BA', { maximumFractionDigits: UNITS[type].decimals })
}

/** The stored KM/kg price as the beekeeper entered it — "150 KM/kg", "4,5 KM/g". */
export function fmtProductPrice(pricePerKg: number, type: HiveProductType): string {
  const value = pricePerKgToDisplay(pricePerKg, type).toLocaleString('bs-BA', { maximumFractionDigits: 3 })
  return `${value} ${priceUnitLabel(type)}`
}

/** Plain number for an input field (dot decimal is what `parseDecimal` accepts either way). */
export function qtyForInput(kg: number, type: HiveProductType): string {
  return String(Number(kgToDisplay(kg, type).toFixed(UNITS[type].decimals)))
}

export function priceForInput(pricePerKg: number, type: HiveProductType): string {
  return String(Number(pricePerKgToDisplay(pricePerKg, type).toFixed(3)))
}

/**
 * kg per product type, in enum order, types without any quantity left out. Deliberately no grand
 * total: 200 g of royal jelly and 20 kg of wax do not add up to anything.
 */
export function sumByType(records: Array<{ productType: HiveProductType; totalKg: number }>) {
  const byType = new Map<HiveProductType, number>()
  for (const r of records) byType.set(r.productType, (byType.get(r.productType) ?? 0) + r.totalKg)
  return HIVE_PRODUCT_TYPES
    .filter(t => (byType.get(t) ?? 0) > 0)
    .map(t => ({ productType: t, kg: byType.get(t)! }))
}

/** A row of a per-apiary, per-pasture or per-hive products table: one name, each product on its own. */
export interface ProductRow {
  name: string
  items: Array<{ productType: HiveProductType; kg: number }>
}

/**
 * The products a table's rows mention, in enum order — its columns. Only what the period has: a column
 * of dashes for a product nobody collected is noise.
 */
export function productColumns(rows: ProductRow[]): HiveProductType[] {
  const present = new Set(rows.flatMap(r => r.items.map(i => i.productType)))
  return HIVE_PRODUCT_TYPES.filter(t => present.has(t))
}

/** The kg of one product in a row, or null when the row has none of it. */
export function productKgIn(row: ProductRow, type: HiveProductType): number | null {
  return row.items.find(i => i.productType === type)?.kg ?? null
}

/** Emoji for chips and cards — one per type, like the rest of the app's section icons. */
export const PRODUCT_ICON: Record<HiveProductType, string> = {
  [HiveProductType.Honey]:      '🍯',
  [HiveProductType.CombHoney]:  '🧇',
  [HiveProductType.Wax]:        '🕯️',
  [HiveProductType.Propolis]:   '🟤',
  [HiveProductType.Pollen]:     '🌼',
  [HiveProductType.RoyalJelly]: '👑',
  [HiveProductType.BeeBread]:   '🍞',
  [HiveProductType.BeeVenom]:   '🐝',
  [HiveProductType.Other]:      '📦',
}
