import { BfzAddress, BfzRecord } from './bfz.model';

/* Option lists and the blank record for the building / floor / zone form. Records come from the API. */

export const COUNTRIES = ['United Arab Emirates', 'Saudi Arabia', 'Qatar', 'Oman', 'Kuwait', 'Bahrain', 'India'];
export const COUNTRY_FLAGS: Partial<Record<string, string>> = {
  'United Arab Emirates': '🇦🇪',
  'Saudi Arabia': '🇸🇦',
  Qatar: '🇶🇦',
  Oman: '🇴🇲',
  Kuwait: '🇰🇼',
  Bahrain: '🇧🇭',
  India: '🇮🇳'
};

export const FLOOR_NAMES: { short: string; name: string; level: number }[] = [
  { short: 'B1', name: 'Basement', level: -1 },
  { short: 'GF', name: 'Ground Floor', level: 0 },
  { short: 'FF', name: 'First Floor', level: 1 },
  { short: 'SF', name: 'Second Floor', level: 2 },
  { short: 'TF', name: 'Third Floor', level: 3 },
  { short: '4F', name: 'Fourth Floor', level: 4 },
  { short: '5F', name: 'Fifth Floor', level: 5 },
  { short: '6F', name: 'Sixth Floor', level: 6 }
];

export const ZONE_COLORS = ['#22c55e', '#f59e0b', '#3b82f6', '#ec4899', '#14b8a6', '#a855f7'];

const NO_ADDRESS: BfzAddress = { line1: '', line2: '', city: '', state: '', country: '', postalCode: '' };

function base(): Omit<BfzRecord, 'code' | 'name' | 'type' | 'siteId'> {
  return {
    buildingCode: null,
    floorCode: null,
    description: '',
    capacity: null,
    area: null,
    status: 'Active',
    image: null,
    images: [],
    category: '',
    levelNumber: null,
    color: ZONE_COLORS[0],
    address: { ...NO_ADDRESS },
    lat: null,
    lng: null,
    allowAssetAssignment: true,
    enableTracking: true,
    restrictedAccess: false,
    showOnMap: true,
    assets: 0,
    people: 0,
    departments: 0,
    createdOn: '',
    updatedOn: ''
  };
}

export function blankBfz(): BfzRecord {
  const now = new Date().toISOString().slice(0, 16);
  return { ...base(), code: '', name: '', type: 'Building', siteId: '', createdOn: now, updatedOn: now };
}
