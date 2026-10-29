export type LocationStatus = 'Active' | 'Inactive';
export type LocationEnvironment = 'Indoor' | 'Outdoor';

export const LOCATION_TYPES = [
  'Area',
  'Room',
  'Rack',
  'Bin',
  'Workstation',
  'Equipment',
  'Parking Bay',
  'Yard',
  'Dock'
] as const;
export type LocationType = (typeof LOCATION_TYPES)[number];

export interface Site {
  id: string;
  name: string;
  lat: number;
  lng: number;
}

export interface Building {
  code: string;
  name: string;
  siteId: string;
  floorCodes: string[];
  lat: number;
  lng: number;
}

export interface Floor {
  code: string;
  name: string;
}

export interface Zone {
  code: string;
  name: string;
  buildingCode: string;
  floorCode: string;
  outdoor: boolean;
}

export interface LocationRecord {
  /** Mongo id from the API; empty until the record is first saved. */
  id: string;
  code: string;
  name: string;
  type: LocationType;
  environment: LocationEnvironment;
  siteId: string;
  buildingCode: string;
  floorCode: string;
  zoneCode: string;
  // Code of the location this one sits inside, e.g. a rack inside a room
  parentCode: string | null;
  area: number | null;
  capacity: number | null;
  description: string;
  status: LocationStatus;
  // Data URL of an uploaded photo
  image: string | null;
  lat: number;
  lng: number;
  restrictedAccess: boolean;
  temperatureControlled: boolean;
  allowAssetAssignment: boolean;
  enableTracking: boolean;
  geofenceAlerts: boolean;
  createdOn: string;
  updatedOn: string;
}

export interface LocationFilters {
  search: string;
  siteId: string;
  buildingCode: string;
  floorCode: string;
  zoneCode: string;
}

export interface LocationStats {
  total: number;
  active: number;
  inactive: number;
  indoor: number;
  outdoor: number;
}
