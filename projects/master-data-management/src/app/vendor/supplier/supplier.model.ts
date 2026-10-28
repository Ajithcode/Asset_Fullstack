import { VendorBase } from '../vendor.model';

export interface SupplierRecord extends VendorBase {
  category: string;
  country: string;
  // 0 to 5
  rating: number;
  // Products & categories
  products: string[];
  assetCategories: string[];
  brands: string[];
  // Financial
  currency: string;
  paymentTerms: string;
  creditLimit: number | null;
  trn: string;
  bankName: string;
  iban: string;
  // Settings
  preferred: boolean;
  approved: boolean;
  portalAccess: boolean;
  emailPurchaseOrders: boolean;
}

export interface SupplierFilters {
  search: string;
  category: string;
  country: string;
  status: '' | SupplierRecord['status'];
}
