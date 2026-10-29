import { AssetModelIcon } from './asset-model.icons';

export type AssetModelStatus = 'Active' | 'Inactive';
export type PmFrequency = 'Monthly' | 'Quarterly' | 'Half-yearly' | 'Yearly' | 'None';
export type MaintenanceStrategy = 'Preventive' | 'Predictive' | 'Condition Based' | 'Run to Failure';
export type Criticality = 'Low' | 'Medium' | 'High' | 'Critical';
export type DepreciationMethod = 'Straight Line' | 'Declining Balance';
export type ProcurementType = 'CAPEX' | 'OPEX';
export type TrackingMethod = 'RFID' | 'BLE' | 'QR Code' | 'Barcode' | 'GPS';
export type AssetCondition = 'New' | 'Good' | 'Fair';

export interface ModelSpec {
  label: string;
  value: string;
}

export interface AssetTypeInfo {
  name: string;
  category: string;
  icon: AssetModelIcon;
  // Used for the default tagging format, e.g. LAP-(YY)-(0000)
  tagPrefix: string;
  lifecycleYears: number;
  warrantyMonths: number;
}

export interface AssetModelRecord {
  code: string;
  name: string;
  manufacturer: string;
  assetType: string;
  category: string;
  description: string;
  // Uploaded picture (data URL); the asset type icon is shown when empty
  image: string | null;
  status: AssetModelStatus;
  specs: ModelSpec[];
  // Lifecycle & maintenance
  lifecycleYears: number;
  warrantyMonths: number;
  pmFrequency: PmFrequency;
  maintenanceStrategy: MaintenanceStrategy;
  criticality: Criticality;
  calibrationRequired: boolean;
  // Financial defaults copied to new assets of this model
  costCenter: string;
  currency: string;
  purchasePrice: number | null;
  includeInDepreciation: boolean;
  depreciationMethod: DepreciationMethod;
  salvagePercent: number;
  procurementType: ProcurementType;
  // Defaults for new assets
  subcategories: string[];
  taggingFormat: string;
  trackingMethod: TrackingMethod;
  defaultCondition: AssetCondition;
  serialRequired: boolean;
  assignable: boolean;
  // Registered assets of this model (owned by the Assets module)
  assetCount: number;
  createdOn: string;
  updatedOn: string;
}

export interface AssetModelFilters {
  search: string;
  manufacturer: string;
  assetType: string;
  status: '' | AssetModelStatus;
}
