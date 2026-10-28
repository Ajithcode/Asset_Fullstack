import { Building, Zone } from './location.model';

// [south, west, north, east] in degrees
export type Bounds = [number, number, number, number];

// Half the footprint of a building (~200 m x 230 m)
const HALF_LAT = 0.0009;
const HALF_LNG = 0.0011;
// Gap kept between neighbouring zones inside a building
const GAP = 0.00006;

export function buildingBounds(building: Building): Bounds {
  return [building.lat - HALF_LAT, building.lng - HALF_LNG, building.lat + HALF_LAT, building.lng + HALF_LNG];
}

/**
 * Lays the zones of one floor out as a grid inside the building footprint, so the map and the
 * floor plan can draw them without real survey data. Zones keep their list order.
 */
export function zoneCells(building: Building, zones: Zone[]): Map<string, Bounds> {
  const cells = new Map<string, Bounds>();
  if (!zones.length) return cells;
  const [south, west, north, east] = buildingBounds(building);
  const cols = zones.length > 1 ? 2 : 1;
  const rows = Math.ceil(zones.length / cols);
  const cellLat = (north - south) / rows;
  const cellLng = (east - west) / cols;
  zones.forEach((zone, i) => {
    const row = Math.floor(i / cols);
    const col = i % cols;
    const top = north - row * cellLat;
    const left = west + col * cellLng;
    cells.set(zone.code, [top - cellLat + GAP, left + GAP, top - GAP, left + cellLng - GAP]);
  });
  return cells;
}

export function center(bounds: Bounds): { lat: number; lng: number } {
  return { lat: (bounds[0] + bounds[2]) / 2, lng: (bounds[1] + bounds[3]) / 2 };
}

/** A smaller box inside a zone cell for one location, nudged by its code so neighbours don't overlap exactly. */
export function locationBounds(cell: Bounds, code: string): Bounds {
  const hash = [...code].reduce((h, c) => (h * 31 + c.charCodeAt(0)) >>> 0, 7);
  const latSpan = cell[2] - cell[0];
  const lngSpan = cell[3] - cell[1];
  const dLat = ((hash % 100) / 100 - 0.5) * latSpan * 0.3;
  const dLng = (((hash >> 8) % 100) / 100 - 0.5) * lngSpan * 0.3;
  const mid = center(cell);
  const halfLat = latSpan * 0.22;
  const halfLng = lngSpan * 0.3;
  return [mid.lat + dLat - halfLat, mid.lng + dLng - halfLng, mid.lat + dLat + halfLat, mid.lng + dLng + halfLng];
}
