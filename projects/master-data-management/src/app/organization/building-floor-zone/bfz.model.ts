export type BfzType = 'Building' | 'Floor' | 'Zone';
export type BfzStatus = 'Active' | 'Inactive';

export const BUILDING_CATEGORIES = ['Factory', 'Office', 'Warehouse', 'Data Center', 'Retail', 'Mixed Use'];
export const ZONE_CATEGORIES = ['Production', 'Maintenance', 'Utility', 'Storage', 'Office', 'Restricted', 'Common Area'];

export interface BfzSite {
  id: string;
  name: string;
  // Prefix used in building codes, e.g. DXB
  prefix: string;
  city: string;
  state: string;
  country: string;
  lat: number;
  lng: number;
}

export interface BfzAddress {
  line1: string;
  line2: string;
  city: string;
  state: string;
  country: string;
  postalCode: string;
}

export interface BfzRecord {
  /** Mongo id from the API; empty until the record is first saved. */
  id: string;
  code: string;
  name: string;
  type: BfzType;
  siteId: string;
  // Set for floors and zones
  buildingCode: string | null;
  // Set for zones
  floorCode: string | null;
  description: string;
  capacity: number | null;
  area: number | null;
  status: BfzStatus;
  // Main photo (data URL or app asset path)
  image: string | null;
  // Extra photos shown under Building Images
  images: string[];
  // Building: usage category; Zone: zone category; Floor: unused
  category: string;
  // Floor: 0 = ground floor, negative = basement
  levelNumber: number | null;
  // Zone fill colour on floor plans
  color: string;
  // Buildings hold the address and position; floors and zones use their building's
  address: BfzAddress;
  lat: number | null;
  lng: number | null;
  allowAssetAssignment: boolean;
  enableTracking: boolean;
  restrictedAccess: boolean;
  showOnMap: boolean;
  // Counts owned by other modules, shown on the building's statistics
  assets: number;
  people: number;
  departments: number;
  createdOn: string;
  updatedOn: string;
}

export interface BfzFilters {
  search: string;
  siteId: string;
  type: '' | BfzType;
  status: '' | BfzStatus;
}
