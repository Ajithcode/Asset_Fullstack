import { ASSET_RECORDS } from 'shared-ui';

/** Asset details the Maintenance pages show (asset panel, location block, pickers). */
export interface CatalogAsset {
  tag: string;
  name: string;
  type: string;
  site: string;
  building: string;
  area: string;
  department: string;
  manufacturer: string;
  model: string;
  serial: string;
  image?: string;
}

/** Main Plant assets from the Maintenance mockups; the backend seed (MaintenanceSeedData.Assets) uses the same list. */
const MAINTENANCE_ASSETS: CatalogAsset[] = [
  { tag: 'GEN-00045', name: 'Generator 500 kVA', type: 'Generator', site: 'Main Plant', building: 'Block A', area: 'Generator Room - G1', department: 'Facilities', manufacturer: 'Cummins', model: 'C500D5', serial: 'SN-2023-00045' },
  { tag: 'AHU-00021', name: 'AHU-01', type: 'AHU', site: 'Head Office', building: 'Building A', area: 'Plant Room - L3', department: 'Facilities', manufacturer: 'Trane', model: 'CSAA-012', serial: 'SN-2022-00021' },
  { tag: 'FL-00078', name: 'Forklift Toyota', type: 'Forklift', site: 'Warehouse', building: 'Main Warehouse', area: 'Loading Bay 2', department: 'Logistics', manufacturer: 'Toyota', model: '8FBE20', serial: 'SN-2021-00078' },
  { tag: 'UPS-00112', name: 'UPS 20 kVA', type: 'UPS', site: 'Head Office', building: 'Building A', area: 'IT Room', department: 'IT', manufacturer: 'APC', model: 'SRT20KXLI', serial: 'SN-2023-00112' },
  { tag: 'FP-00033', name: 'Fire Pump', type: 'Fire Pump', site: 'Main Plant', building: 'Utility Block', area: 'Utility Area', department: 'Safety', manufacturer: 'Grundfos', model: 'NKF 80-250', serial: 'SN-2020-00033' },
  { tag: 'CAM-00089', name: 'CCTV Camera', type: 'IP Camera', site: 'Head Office', building: 'Main Building', area: 'Main Entrance', department: 'Security', manufacturer: 'Hikvision', model: 'DS-2CD2143', serial: 'SN-2024-00089' },
  { tag: 'SW-00176', name: 'Network Switch', type: 'Network Switch', site: 'Head Office', building: 'Data Centre', area: 'Rack B-04', department: 'IT', manufacturer: 'Cisco', model: 'C9300-48P', serial: 'SN-2023-00176' },
  { tag: 'CH-00012', name: 'Chiller Plant', type: 'Chiller', site: 'Main Plant', building: 'Block B', area: 'Chiller Yard', department: 'Facilities', manufacturer: 'Carrier', model: '30XA-502', serial: 'SN-2019-00012' },
  { tag: 'ACP-00056', name: 'Access Control Panel', type: 'Access Control', site: 'Head Office', building: 'Main Building', area: 'Security Room', department: 'Security', manufacturer: 'HID', model: 'VertX V1000', serial: 'SN-2022-00056' },
  { tag: 'AC-00100', name: 'Air Compressor', type: 'Air Compressor', site: 'Main Plant', building: 'Maintenance Block', area: 'Compressor Room', department: 'Maintenance', manufacturer: 'Atlas Copco', model: 'GA 37 VSD', serial: 'SN-2021-00100' },
  { tag: 'HVAC-00201', name: 'HVAC Unit 1', type: 'HVAC Unit', site: 'Head Office', building: 'Building B', area: 'Roof', department: 'Facilities', manufacturer: 'Daikin', model: 'RXYQ20', serial: 'SN-2022-00201' },
  { tag: 'HVAC-00202', name: 'HVAC Unit 2', type: 'HVAC Unit', site: 'Head Office', building: 'Building B', area: 'Roof', department: 'Facilities', manufacturer: 'Daikin', model: 'RXYQ20', serial: 'SN-2022-00202' },
  { tag: 'HVAC-00203', name: 'HVAC Unit 3', type: 'HVAC Unit', site: 'Main Plant', building: 'Block A', area: 'Roof', department: 'Facilities', manufacturer: 'Daikin', model: 'RXYQ16', serial: 'SN-2023-00203' },
  { tag: 'ELV-00011', name: 'Elevator 1', type: 'Elevator', site: 'Head Office', building: 'Main Building', area: 'Lift Core A', department: 'Facilities', manufacturer: 'Otis', model: 'Gen2 Life', serial: 'SN-2020-00011' },
  { tag: 'GEN-00052', name: 'Gas Generator 750 kVA', type: 'Generator', site: 'Main Plant', building: 'Substation', area: 'Outdoor', department: 'Engineering', manufacturer: 'Caterpillar', model: 'G3512', serial: 'SN-2021-00052' },
  { tag: 'UPS-00118', name: 'UPS 60 kVA', type: 'UPS', site: 'Head Office', building: 'Data Centre', area: 'UPS Room', department: 'IT', manufacturer: 'Eaton', model: '93PM-60', serial: 'SN-2022-00118' },
  { tag: 'CH-00014', name: 'Chiller Unit 2', type: 'Chiller', site: 'Main Plant', building: 'Block B', area: 'Chiller Yard', department: 'Facilities', manufacturer: 'Carrier', model: '30XA-402', serial: 'SN-2020-00014' },
  { tag: 'AC-00104', name: 'Air Compressor 2', type: 'Air Compressor', site: 'Main Plant', building: 'Maintenance Block', area: 'Compressor Room', department: 'Maintenance', manufacturer: 'Atlas Copco', model: 'GA 22', serial: 'SN-2022-00104' }
];

/** Mockup assets first, then the shared Asset List records not already present. */
export const ASSET_CATALOG: CatalogAsset[] = [
  ...MAINTENANCE_ASSETS,
  ...ASSET_RECORDS.filter((r) => !MAINTENANCE_ASSETS.some((a) => a.tag === r.tag)).map((r) => {
    const [building, ...rest] = r.area.split(' - ');
    return {
      tag: r.tag,
      name: r.name,
      type: r.type,
      site: r.site,
      building: rest.length ? building! : '',
      area: rest.length ? rest.join(' - ') : r.area,
      department: r.department,
      manufacturer: r.manufacturer,
      model: r.model,
      serial: r.serialNumber ?? '',
      image: r.image
    };
  })
];

export function findCatalogAsset(tag: string | null | undefined): CatalogAsset | undefined {
  const t = (tag ?? '').trim().toLowerCase();
  return t ? ASSET_CATALOG.find((a) => a.tag.toLowerCase() === t) : undefined;
}
