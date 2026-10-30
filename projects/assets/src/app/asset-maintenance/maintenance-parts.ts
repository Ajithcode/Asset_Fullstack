/** Spare parts for one asset: catalogue, usage lines (each tied to a work order), reservations and stock status. */

export interface SparePart {
  partNo: string;
  name: string;
  category: string;
  /** Current catalogue price (AED). */
  unitCost: number;
  onHand: number;
  reorderLevel: number;
  /** Requested but not yet received. */
  onOrder: number;
  leadTimeDays: number;
  manufacturer: string;
  compatible: string;
  critical: boolean;
  /** Quantity the asset's bill of materials recommends keeping. */
  bomQty: number;
  /** Typical replacement interval; 0 = replaced on condition. */
  replaceEveryDays: number;
}

/** One issue of a part to a work order; the lines of a work order add up to its parts cost. */
export interface PartUsage {
  wo: string;
  date: string;
  partNo: string;
  qty: number;
  /** Price at the time of issue. */
  unitCost: number;
}

export interface PartReservation {
  partNo: string;
  qty: number;
  for: string;
  neededBy: string;
}

export type StockStatus = 'In Stock' | 'Low Stock' | 'Out of Stock';

/** Workshop consumables and other non-stocked lines (no reorder level, nothing on the BOM). */
export function isStocked(p: SparePart): boolean {
  return p.reorderLevel > 0 || p.bomQty > 0;
}

export function stockStatus(p: SparePart): StockStatus {
  if (p.onHand <= 0) return 'Out of Stock';
  return p.onHand <= p.reorderLevel ? 'Low Stock' : 'In Stock';
}

export function reservedQty(partNo: string, reserved: PartReservation[]): number {
  return reserved.filter((r) => r.partNo === partNo).reduce((t, r) => t + r.qty, 0);
}

export function lineCost(u: PartUsage): number {
  return u.qty * u.unitCost;
}
