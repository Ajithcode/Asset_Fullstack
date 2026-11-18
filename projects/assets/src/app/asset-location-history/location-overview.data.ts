import { AssetListItemDto } from '../asset-maintenance/asset-lookup.service';
import { LocationEventDto } from './location-history.service';

/** Static sample data for the Location History overview (no API calls). */

const SITES: Record<string, string[]> = {
  'Main Plant': ['Block A – Outdoor Yard', 'Maintenance Workshop', 'Pump Room 1', 'Pump Room 2', 'Boiler House', 'Control Room'],
  Warehouse: ['Loading Bay 1', 'Loading Bay 2', 'Storage Zone', 'Dispatch Area', 'Cold Store'],
  'Head Office': ['IT Department', 'Finance Office', 'Reception', 'Conference Room A', 'HR Office'],
  Factory: ['Assembly Line 1', 'Assembly Line 2', 'Assembly Line 3', 'Paint Shop', 'QC Lab'],
};

/** prefix, name, type, category, site, manufacturer */
const KINDS: [string, string, string, string, string, string][] = [
  ['GEN', 'Diesel Generator 250 kVA', 'Generator', 'Power Equipment', 'Main Plant', 'Cummins'],
  ['FLT', 'Electric Forklift', 'Forklift', 'Material Handling', 'Warehouse', 'Toyota'],
  ['PMP', 'Water Pump', 'Pump', 'Mechanical', 'Main Plant', 'Grundfos'],
  ['LPT', 'Dell Latitude Laptop', 'IT Equipment', 'IT Assets', 'Head Office', 'Dell'],
  ['TRK', 'Material Trolley', 'Trolley', 'Material Handling', 'Factory', 'Kaiser'],
  ['CMP', 'Air Compressor', 'Compressor', 'Mechanical', 'Factory', 'Atlas Copco'],
  ['WLD', 'MIG Welding Machine', 'Welder', 'Tools', 'Factory', 'Lincoln'],
  ['PRN', 'HP LaserJet Printer', 'IT Equipment', 'IT Assets', 'Head Office', 'HP'],
  ['PLT', 'Pallet Jack', 'Pallet Jack', 'Material Handling', 'Warehouse', 'Crown'],
  ['MTR', 'Induction Motor 75 kW', 'Motor', 'Electrical', 'Main Plant', 'Siemens'],
];

const USERS = ['Ravi Kumar', 'Priya Sharma', 'Arjun Mehta', 'Sneha Iyer'];
const STATUSES = ['Active', 'Active', 'Active', 'Active', 'Active', 'In Maintenance', 'Active', 'Inactive'];

type Src = 'BLE' | 'Manual' | 'QR Scan' | 'GPS' | 'RFID';
const DETAILS: Record<Src, string> = {
  BLE: 'Detected by BLE gateway',
  Manual: 'Manual location update',
  'QR Scan': 'QR scan at checkpoint',
  GPS: 'GPS tag position fix',
  RFID: 'RFID gate read',
};

/** Deterministic random numbers, so the page looks the same on every visit. */
function rng(seed: number): () => number {
  return () => {
    seed = (seed + 0x6d2b79f5) | 0;
    let t = Math.imul(seed ^ (seed >>> 15), 1 | seed);
    t = (t + Math.imul(t ^ (t >>> 7), 61 | t)) ^ t;
    return ((t ^ (t >>> 14)) >>> 0) / 4294967296;
  };
}

function pickSource(r: number): Src {
  return r < 0.46 ? 'BLE' : r < 0.74 ? 'Manual' : r < 0.87 ? 'QR Scan' : r < 0.95 ? 'GPS' : 'RFID';
}

/** Latest two movements of the assets in the design (previous → current), days ago + time. */
const FEATURED: Record<string, { prev: string; cur: string; daysAgo: number; time: [number, number]; source: Src }> = {
  'GEN-00045': { prev: 'Maintenance Workshop', cur: 'Block A – Outdoor Yard', daysAgo: 0, time: [10, 25], source: 'Manual' },
  'FLT-00128': { prev: 'Storage Zone', cur: 'Loading Bay 2', daysAgo: 0, time: [9, 40], source: 'BLE' },
  'PMP-00214': { prev: 'Pump Room 1', cur: 'Pump Room 2', daysAgo: 1, time: [16, 10], source: 'QR Scan' },
  'LPT-00356': { prev: 'Finance Office', cur: 'IT Department', daysAgo: 1, time: [11, 30], source: 'Manual' },
  'TRK-00078': { prev: 'Assembly Line 2', cur: 'Assembly Line 3', daysAgo: 2, time: [13, 20], source: 'BLE' },
};
const FEATURED_NUMBERS: Record<string, number> = { GEN: 45, FLT: 128, PMP: 214, LPT: 356, TRK: 78 };

export function buildLocationSample(now = new Date()): { assets: AssetListItemDto[]; events: LocationEventDto[] } {
  const rand = rng(360);
  const assets: AssetListItemDto[] = [];
  const events: LocationEventDto[] = [];
  const today = new Date(now.getFullYear(), now.getMonth(), now.getDate());
  let eventNo = 1;

  const push = (tag: string, name: string, at: Date, zone: string, source: Src) => {
    const manual = source === 'Manual';
    events.push({
      id: `ev-${eventNo}`,
      eventId: `LE-${String(eventNo++).padStart(5, '0')}`,
      eventTime: at.toISOString(),
      assetId: tag,
      assetName: name,
      eventType: 'Entered Zone',
      zoneName: zone,
      details: DETAILS[source],
      createdBy: manual ? USERS[Math.floor(rand() * USERS.length)] : 'System',
      createdAt: at.toISOString(),
    });
  };

  for (let i = 0; i < 40; i++) {
    const [prefix, name, type, category, site, maker] = KINDS[i % KINDS.length];
    const num = i < KINDS.length && FEATURED_NUMBERS[prefix] ? FEATURED_NUMBERS[prefix] : 101 + i * 23;
    const tag = `${prefix}-${String(num).padStart(5, '0')}`;
    const zones = SITES[site];
    const featured = FEATURED[tag];

    // Older movements over the last ~60 days
    const count = 3 + Math.floor(rand() * 9);
    let zone = zones[Math.floor(rand() * zones.length)];
    const times = Array.from({ length: count }, () => today.getTime() - (3 + rand() * 58) * 86_400_000).sort((a, b) => a - b);
    for (const t of times) {
      let next = zones[Math.floor(rand() * zones.length)];
      if (next === zone) next = zones[(zones.indexOf(next) + 1) % zones.length];
      zone = next;
      push(tag, name, new Date(t), zone, pickSource(rand()));
    }
    // Latest movement: the design's rows, else a recent one (some today)
    if (featured) {
      const at = new Date(today.getTime() - featured.daysAgo * 86_400_000);
      at.setHours(...featured.time);
      push(tag, name, new Date(at.getTime() - 20 * 3_600_000), featured.prev, 'BLE');
      push(tag, name, at, featured.cur, featured.source);
      zone = featured.cur;
    } else if (rand() < 0.6) {
      const at = new Date(today.getTime() - Math.floor(rand() * 3) * 86_400_000);
      at.setHours(7 + Math.floor(rand() * 10), Math.floor(rand() * 60));
      if (at <= now) {
        const next = zones[(zones.indexOf(zone) + 1) % zones.length];
        push(tag, name, at, next, pickSource(rand()));
        zone = next;
      }
    }

    // Registered location: the latest tracked zone, except a few (shown as unverified)
    const registeredZone = i % 9 === 4 ? zones[(zones.indexOf(zone) + 2) % zones.length] : zone;
    assets.push({
      id: `a-${i + 1}`,
      assetRegistrationId: `AR-${String(i + 1).padStart(5, '0')}`,
      assetName: name,
      assetTag: tag,
      imageUrl: null,
      category,
      assetType: type,
      siteName: site,
      buildingName: null,
      floorName: null,
      zoneName: registeredZone,
      roomName: null,
      locationPath: `${site} / ${registeredZone}`,
      custodianUserId: null,
      custodianName: USERS[i % USERS.length],
      department: null,
      status: featured ? 'Active' : STATUSES[i % STATUSES.length],
      criticality: 'Medium',
      manufacturer: maker,
      nextMaintenanceDate: null,
      createdAt: null,
    });
  }
  return { assets, events };
}

/** Site centres (static); zones sit in a ring around their site. */
const SITE_CENTERS: Record<string, [number, number]> = {
  'Main Plant': [13.0985, 80.1625],
  Warehouse: [13.1012, 80.1668],
  'Head Office': [13.0952, 80.1681],
  Factory: [13.0958, 80.1589],
};

export interface ZonePoint {
  zone: string;
  site: string;
  lat: number;
  lng: number;
}

export const SITE_POINTS = Object.entries(SITE_CENTERS).map(([site, [lat, lng]]) => ({ site, lat, lng }));

/** Map position of every sample zone. */
export const ZONE_POINTS: ZonePoint[] = Object.entries(SITES).flatMap(([site, zones]) => {
  const [lat, lng] = SITE_CENTERS[site];
  return zones.map((zone, i) => {
    const a = (i / zones.length) * 2 * Math.PI;
    return { zone, site, lat: lat + Math.sin(a) * 0.0007, lng: lng + Math.cos(a) * 0.0008 };
  });
});
