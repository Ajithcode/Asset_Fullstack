export type RecordStatus = 'Active' | 'Inactive';

export interface ItemCategoryRecord {
  code: string;
  name: string;
  parentCode: string | null;
  description: string;
  status: RecordStatus;
  // Settings
  defaultUom: string;
  valuationMethod: string;
  inspectionOnReceipt: boolean;
  allowNegativeStock: boolean;
  createdOn: string;
  updatedOn: string;
}

export type UomType = 'Discrete' | 'Weight' | 'Volume' | 'Length' | 'Area' | 'Time' | 'Energy';

export interface UomRecord {
  code: string;
  name: string;
  type: UomType;
  symbol: string;
  // 1 <this unit> = conversionFactor <baseUom>; the base unit of a type has factor 1 and no base
  baseUom: string;
  conversionFactor: number;
  description: string;
  status: RecordStatus;
  allowDecimals: boolean;
  decimalPlaces: number;
  createdOn: string;
  updatedOn: string;
}

export type StockStatusType = 'Available' | 'Warning' | 'Unavailable' | 'Allocated' | 'In Transit' | 'On Hold' | 'Closed';

export interface StockStatusRecord {
  code: string;
  name: string;
  type: StockStatusType;
  color: string;
  description: string;
  usage: string;
  isDefault: boolean;
  status: RecordStatus;
  // Usage & rules
  canIssue: boolean;
  canReserve: boolean;
  countsAsAvailable: boolean;
  triggersReorder: boolean;
  // Settings
  showInReports: boolean;
  notifyStorekeeper: boolean;
  createdOn: string;
  updatedOn: string;
}

export interface WarehouseRecord {
  code: string;
  name: string;
  location: string;
  // Employee ID
  managerId: string;
  description: string;
  status: RecordStatus;
  address: { line1: string; city: string; state: string; postalCode: string; country: string };
  phone: string;
  email: string;
  areaSqm: number | null;
  operatingHours: string;
  allowTransfersOut: boolean;
  createdOn: string;
  updatedOn: string;
}

export interface StoreRecord {
  code: string;
  name: string;
  warehouseCode: string;
  storeType: string;
  // Employee ID
  keeperId: string;
  description: string;
  status: RecordStatus;
  temperatureControlled: boolean;
  restrictedAccess: boolean;
  createdOn: string;
  updatedOn: string;
}

export interface BinRecord {
  code: string;
  storeCode: string;
  rack: string;
  shelf: string;
  binType: string;
  // Max units the bin holds; null when not tracked
  capacity: number | null;
  description: string;
  status: RecordStatus;
  createdOn: string;
  updatedOn: string;
}

export interface ItemRecord {
  code: string;
  name: string;
  partNumber: string;
  // Item category code and UOM code
  category: string;
  uom: string;
  description: string;
  image: string | null;
  status: RecordStatus;
  // Inventory
  currentStock: number;
  minStock: number;
  reorderLevel: number;
  reorderQty: number;
  maxStock: number | null;
  warehouseCode: string;
  storeCode: string;
  binCode: string;
  // Financial
  unitCost: number | null;
  currency: string;
  valuationMethod: string;
  // Additional
  manufacturer: string;
  specification: string;
  shelfLifeDays: number | null;
  hazardous: boolean;
  serialized: boolean;
  batchTracked: boolean;
  // Settings
  purchasable: boolean;
  issuable: boolean;
  criticalSpare: boolean;
  createdOn: string;
  updatedOn: string;
}
