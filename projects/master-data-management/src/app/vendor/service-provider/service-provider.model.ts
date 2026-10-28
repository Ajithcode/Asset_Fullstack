import { VendorBase } from '../vendor.model';

export interface ServiceProviderRecord extends VendorBase {
  serviceType: string;
  country: string;
  // 0 to 5
  rating: number;
  // Services & capabilities
  services: string[];
  skills: string[];
  coverage: string[];
  technicians: number;
  responseHours: number;
  // Qualification & compliance
  licenceNo: string;
  licenceExpiry: string;
  insurancePolicy: string;
  insuranceExpiry: string;
  isoCertified: boolean;
  hseApproved: boolean;
  // Settings
  preferred: boolean;
  approved: boolean;
  portalAccess: boolean;
  emergencyCallout: boolean;
}

export interface ServiceProviderFilters {
  search: string;
  serviceType: string;
  country: string;
  status: '' | ServiceProviderRecord['status'];
}
