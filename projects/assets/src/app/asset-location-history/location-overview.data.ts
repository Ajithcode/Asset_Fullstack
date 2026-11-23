/** Static map layout for the Location History overview map (sites and their zones). */

const SITES: Record<string, string[]> = {
  'Main Plant': ['Block A – Outdoor Yard', 'Maintenance Workshop', 'Pump Room 1', 'Pump Room 2', 'Boiler House', 'Control Room'],
  Warehouse: ['Loading Bay 1', 'Loading Bay 2', 'Storage Zone', 'Dispatch Area', 'Cold Store'],
  'Head Office': ['IT Department', 'Finance Office', 'Reception', 'Conference Room A', 'HR Office'],
  Factory: ['Assembly Line 1', 'Assembly Line 2', 'Assembly Line 3', 'Paint Shop', 'QC Lab'],
};

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
