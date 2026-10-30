import { blankVendorBase } from '../vendor.data';
import { ServiceProviderRecord } from './service-provider.model';

export const SERVICE_TYPES = [
  'Installation', 'Maintenance', 'Facilities Management', 'Inspection', 'Calibration', 'Cleaning', 'Security Services',
  'Pest Control', 'Testing & Commissioning', 'Repairs'
];
export const COVERAGE_AREAS = ['Dubai', 'Abu Dhabi', 'Sharjah', 'Ajman', 'Ras Al Khaimah', 'Fujairah', 'Umm Al Quwain', 'Oman', 'Qatar', 'Saudi Arabia'];

export function blankServiceProvider(): ServiceProviderRecord {
  return {
    ...blankVendorBase(),
    serviceType: '',
    country: '',
    rating: 0,
    services: [],
    skills: [],
    coverage: [],
    technicians: 0,
    responseHours: 24,
    licenceNo: '',
    licenceExpiry: '',
    insurancePolicy: '',
    insuranceExpiry: '',
    isoCertified: false,
    hseApproved: false,
    preferred: false,
    approved: false,
    portalAccess: false,
    emergencyCallout: false
  };
}
