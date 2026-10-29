export const SITE_TYPES = ['Plant', 'Office', 'Warehouse', 'Field Site', 'Data Center', 'Retail Store', 'Yard'] as const;
export type SiteType = (typeof SITE_TYPES)[number];
export type SiteStatus = 'Active' | 'Inactive';

export const WEEK_DAYS = ['Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat', 'Sun'];

export interface SiteContact {
  name: string;
  designation: string;
  email: string;
  phoneCountry: string;
  phone: string;
}

export interface SiteRecord {
  // Stable id other masters refer to (e.g. buildings)
  id: string;
  code: string;
  name: string;
  siteType: SiteType;
  // Code of the owning organization in the Organization master
  organizationCode: string;
  businessUnit: string;
  department: string;
  parentId: string | null;
  timeZone: string;
  description: string;
  status: SiteStatus;
  image: string | null;
  images: string[];
  address: { line1: string; line2: string; city: string; state: string; country: string; postalCode: string };
  lat: number | null;
  lng: number | null;
  manager: SiteContact;
  emergency: SiteContact;
  openTime: string;
  closeTime: string;
  workingDays: string[];
  open24x7: boolean;
  allowAssetAssignment: boolean;
  enableTracking: boolean;
  // Counts owned by other modules
  assets: number;
  people: number;
  createdOn: string;
  updatedOn: string;
}

export interface SiteFilters {
  search: string;
  status: '' | SiteStatus;
  siteType: '' | SiteType;
  businessUnit: string;
}
