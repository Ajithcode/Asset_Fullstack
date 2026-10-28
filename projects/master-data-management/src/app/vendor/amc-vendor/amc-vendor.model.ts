import { VendorBase } from '../vendor.model';

export interface AmcVendorRecord extends VendorBase {
  assetType: string;
  contractType: string;
  // YYYY-MM-DD
  startDate: string;
  endDate: string;
  // Contract details
  contractNo: string;
  contractValue: number | null;
  currency: string;
  billingCycle: string;
  renewalNoticeDays: number;
  autoRenew: boolean;
  // Asset coverage: asset model codes
  coveredModels: string[];
  visitsPerYear: number;
  // Service level
  responseHours: number;
  resolutionHours: number;
  uptimePercent: number;
  penaltyPerHour: number | null;
  // Settings
  notifyBeforeExpiry: boolean;
  serviceReportRequired: boolean;
}

export interface AmcVendorFilters {
  search: string;
  assetType: string;
  contractType: string;
  status: '' | AmcVendorRecord['status'];
}
