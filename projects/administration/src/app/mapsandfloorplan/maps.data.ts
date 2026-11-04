import {
  Bluetooth,
  Briefcase,
  Building,
  Building2,
  Cctv,
  Factory,
  Fan,
  Gauge,
  Hand,
  Layers,
  LucideIconData,
  Package,
  Radio,
  Snowflake,
  Thermometer,
  Type,
  Warehouse,
  Wifi,
  Wrench,
  Zap
} from 'lucide-angular';

// DEMO MODE: Maps & Floor Plans keeps its records in memory until the maps API exists

export type Tone = 'purple' | 'blue' | 'pink' | 'green' | 'orange' | 'red' | 'grey';
export type MapStatus = 'Published' | 'Draft' | 'Archived';

/* ===== Tabs (one per sub page in ADMINISTRATION_MENU > Maps & Floor Plans) ===== */

export type MapsTabId =
  | 'map-library'
  | 'floor-plans'
  | 'zones-geofences'
  | 'device-asset-plotting'
  | 'layers'
  | 'versions-publishing'
  | 'settings';

/* ===== Site tree ===== */

export interface SiteNode {
  id: string;
  label: string;
  kind: 'org' | 'site' | 'building' | 'floor';
  count?: number;
  children?: SiteNode[];
}

export const SITE_TREE: SiteNode = {
  id: 'org',
  label: 'Purple IQ Organization',
  kind: 'org',
  children: [
    {
      id: 'ho',
      label: 'Head Office (HO)',
      kind: 'site',
      children: [
        {
          id: 'ho-a',
          label: 'Building A',
          kind: 'building',
          children: [
            { id: 'ho-a-f1', label: 'Floor 1', kind: 'floor', count: 2 },
            { id: 'ho-a-f2', label: 'Floor 2', kind: 'floor', count: 1 },
            { id: 'ho-a-f3', label: 'Floor 3', kind: 'floor', count: 1 }
          ]
        },
        {
          id: 'ho-b',
          label: 'Building B',
          kind: 'building',
          children: [
            { id: 'ho-b-f1', label: 'Floor 1', kind: 'floor', count: 1 },
            { id: 'ho-b-f2', label: 'Floor 2', kind: 'floor', count: 0 }
          ]
        }
      ]
    },
    {
      id: 'p1',
      label: 'Plant 1',
      kind: 'site',
      children: [{ id: 'p1-g', label: 'Ground Floor', kind: 'floor', count: 1 }]
    },
    {
      id: 'wh',
      label: 'Warehouse',
      kind: 'site',
      children: [{ id: 'wh-g', label: 'Ground Floor', kind: 'floor', count: 1 }]
    }
  ]
};

export const KIND_ICONS: Record<SiteNode['kind'], LucideIconData> = {
  org: Building2,
  site: Building2,
  building: Building,
  floor: Layers
};

/* ===== Floor plans + versions ===== */

export interface FloorPlan {
  id: string;
  floorId: string;
  name: string;
  subtitle: string;
  type: 'Floor Plan' | 'Site Map';
  format: 'PNG' | 'JPG' | 'PDF' | 'SVG' | 'DWG';
  sizeMb: number;
  version: string;
  status: MapStatus;
  updated: string;
  updatedBy: string;
  dimensions: string;
  scale: string;
  fileName: string;
}

export const FLOOR_PLANS: FloorPlan[] = [
  {
    id: 'fp-1', floorId: 'ho-a-f1', name: 'Building A - Floor 1', subtitle: 'Main production floor', type: 'Floor Plan', format: 'PNG',
    sizeMb: 2.4, version: 'v2.1', status: 'Published', updated: '2026-01-15T10:24:00', updatedBy: 'John Doe',
    dimensions: '60.00 m × 32.00 m', scale: '1 : 100', fileName: 'Floor1_v2.1.png'
  },
  {
    id: 'fp-2', floorId: 'ho-a-f1', name: 'Building A - Floor 1 (BIM)', subtitle: 'Detailed layout (CAD)', type: 'Floor Plan', format: 'DWG',
    sizeMb: 8.7, version: 'v1.0', status: 'Draft', updated: '2026-01-10T15:15:00', updatedBy: 'Sarah Lee',
    dimensions: '60.00 m × 32.00 m', scale: '1 : 50', fileName: 'Floor1_BIM_v1.0.dwg'
  },
  {
    id: 'fp-3', floorId: 'ho-a-f2', name: 'Building A - Floor 2', subtitle: 'Offices and meeting rooms', type: 'Floor Plan', format: 'PNG',
    sizeMb: 1.9, version: 'v1.2', status: 'Published', updated: '2025-12-02T09:10:00', updatedBy: 'John Doe',
    dimensions: '60.00 m × 32.00 m', scale: '1 : 100', fileName: 'Floor2_v1.2.png'
  },
  {
    id: 'fp-4', floorId: 'ho-a-f3', name: 'Building A - Floor 3', subtitle: 'Server room and storage', type: 'Floor Plan', format: 'PDF',
    sizeMb: 3.1, version: 'v1.0', status: 'Published', updated: '2025-11-20T14:40:00', updatedBy: 'Priya Sharma',
    dimensions: '60.00 m × 32.00 m', scale: '1 : 100', fileName: 'Floor3_v1.0.pdf'
  },
  {
    id: 'fp-5', floorId: 'ho-b-f1', name: 'Building B - Floor 1', subtitle: 'Workshop', type: 'Floor Plan', format: 'JPG',
    sizeMb: 2.2, version: 'v1.1', status: 'Draft', updated: '2026-01-03T11:00:00', updatedBy: 'Omar Hassan',
    dimensions: '48.00 m × 30.00 m', scale: '1 : 100', fileName: 'BldB_Floor1_v1.1.jpg'
  },
  {
    id: 'fp-6', floorId: 'p1-g', name: 'Plant 1 - Site Map', subtitle: 'Plant overview', type: 'Site Map', format: 'SVG',
    sizeMb: 0.8, version: 'v3.0', status: 'Published', updated: '2025-10-11T08:30:00', updatedBy: 'James Tan',
    dimensions: '220.00 m × 140.00 m', scale: '1 : 500', fileName: 'Plant1_site_v3.0.svg'
  },
  {
    id: 'fp-7', floorId: 'wh-g', name: 'Warehouse - Ground', subtitle: 'Racking layout', type: 'Floor Plan', format: 'PNG',
    sizeMb: 4.6, version: 'v2.0', status: 'Published', updated: '2025-12-18T16:05:00', updatedBy: 'Fatima Noor',
    dimensions: '90.00 m × 55.00 m', scale: '1 : 200', fileName: 'Warehouse_v2.0.png'
  }
];

export interface MapVersion {
  version: string;
  current: boolean;
  status: MapStatus;
  changes: string[];
  uploadedBy: string;
  uploadedOn: string;
  fileName: string;
  sizeMb: number;
  description: string;
}

export const VERSIONS: MapVersion[] = [
  {
    version: 'v2.1', current: true, status: 'Published', uploadedBy: 'John Doe', uploadedOn: '2026-01-15T10:24:00',
    changes: ['Updated production zone layout', 'Added 2 new asset plots (CH-001, AHU-01)', 'Refined zone boundaries'],
    fileName: 'BuildingA_Floor1_v2.1.png', sizeMb: 2.4,
    description: 'Updated production zone layout, added 2 new asset plots and refined zone boundaries.'
  },
  {
    version: 'v2.0', current: false, status: 'Draft', uploadedBy: 'Sarah Lee', uploadedOn: '2026-01-10T15:15:00',
    changes: ['Added storage zone (SZ-01)', 'Updated device locations', 'Minor layout adjustments'],
    fileName: 'BuildingA_Floor1_v2.0.png', sizeMb: 2.3, description: 'Added the storage zone and moved gateways to their surveyed positions.'
  },
  {
    version: 'v1.0', current: false, status: 'Archived', uploadedBy: 'John Doe', uploadedOn: '2026-01-05T11:20:00',
    changes: ['Initial floor plan upload', 'Basic zones and locations'],
    fileName: 'BuildingA_Floor1_v1.0.png', sizeMb: 2.1, description: 'Initial floor plan upload with the basic zones.'
  }
];

/* ===== Zones ===== */

export type ZoneType = 'Production' | 'Office' | 'Storage' | 'Restricted' | 'Maintenance' | 'Operational';

export interface MapZone {
  id: string;
  code: string;
  name: string;
  type: ZoneType;
  tone: Tone;
  shape: 'Polygon' | 'Rectangle' | 'Circle' | 'Custom';
  // Position on the plan, in % of its width / height
  x: number;
  y: number;
  w: number;
  h: number;
  areaM2: number;
  description: string;
  accessRule: string;
  active: boolean;
  entryAlert: boolean;
  exitAlert: boolean;
  dwellAlert: boolean;
  dwellMinutes: number;
  geofence: boolean;
}

export const ZONE_TYPES: { type: ZoneType; tone: Tone; icon: LucideIconData }[] = [
  { type: 'Production', tone: 'blue', icon: Factory },
  { type: 'Office', tone: 'orange', icon: Briefcase },
  { type: 'Storage', tone: 'green', icon: Package },
  { type: 'Restricted', tone: 'red', icon: Hand },
  { type: 'Maintenance', tone: 'purple', icon: Wrench },
  { type: 'Operational', tone: 'grey', icon: Gauge }
];

export const ACCESS_RULES = ['Authorized Personnel Only', 'All Staff', 'Contractors with Escort', 'Restricted - Badge Required'];

export const ZONES: MapZone[] = [
  {
    id: 'z-oz1', code: 'OZ-01', name: 'Office Zone', type: 'Office', tone: 'orange', shape: 'Rectangle', x: 3, y: 12, w: 15, h: 31, areaM2: 142.5,
    description: 'Administration offices.', accessRule: 'All Staff', active: true, entryAlert: false, exitAlert: false, dwellAlert: false, dwellMinutes: 30, geofence: false
  },
  {
    id: 'z-oz2', code: 'OZ-02', name: 'Office Zone', type: 'Office', tone: 'orange', shape: 'Rectangle', x: 3, y: 45, w: 15, h: 26, areaM2: 120,
    description: 'Engineering offices.', accessRule: 'All Staff', active: true, entryAlert: false, exitAlert: false, dwellAlert: false, dwellMinutes: 30, geofence: false
  },
  {
    id: 'z-pz1', code: 'PZ-01', name: 'Production Zone', type: 'Production', tone: 'blue', shape: 'Polygon', x: 24, y: 12, w: 41, h: 54, areaM2: 1280.5,
    description: 'Main production area with equipment and assembly lines.', accessRule: 'Authorized Personnel Only', active: true,
    entryAlert: true, exitAlert: true, dwellAlert: false, dwellMinutes: 30, geofence: true
  },
  {
    id: 'z-sz1', code: 'SZ-01', name: 'Storage Zone', type: 'Storage', tone: 'green', shape: 'Rectangle', x: 70, y: 15, w: 25, h: 27, areaM2: 310,
    description: 'Raw material and spares storage.', accessRule: 'All Staff', active: true, entryAlert: false, exitAlert: true, dwellAlert: false, dwellMinutes: 30, geofence: true
  },
  {
    id: 'z-rz1', code: 'RZ-01', name: 'Restricted Zone', type: 'Restricted', tone: 'red', shape: 'Rectangle', x: 70, y: 45, w: 25, h: 24, areaM2: 260,
    description: 'Electrical room and chemical store.', accessRule: 'Restricted - Badge Required', active: true,
    entryAlert: true, exitAlert: true, dwellAlert: true, dwellMinutes: 15, geofence: true
  },
  {
    id: 'z-mz1', code: 'MZ-01', name: 'Maintenance Zone', type: 'Maintenance', tone: 'purple', shape: 'Rectangle', x: 30, y: 74, w: 27, h: 18, areaM2: 185,
    description: 'Maintenance workshop and tool crib.', accessRule: 'Authorized Personnel Only', active: true,
    entryAlert: false, exitAlert: false, dwellAlert: false, dwellMinutes: 30, geofence: false
  }
];

/* ===== Devices + assets plotted on the plan ===== */

export type PlotKind = 'device' | 'asset';

export interface PlotItem {
  id: string;
  code: string;
  name: string;
  kind: PlotKind;
  // Device type (Gateway, Sensor...) or asset category (HVAC...)
  type: string;
  assetType?: string;
  model?: string;
  serial?: string;
  icon: LucideIconData;
  tone: Tone;
  status: 'Active' | 'Inactive' | 'Offline';
  // Position on the plan in %; null when not plotted yet
  x: number | null;
  y: number | null;
  zoneId?: string;
  orientation: number;
  lastUpdated: string;
}

export const PLOT_ITEMS: PlotItem[] = [
  { id: 'd-gw01', code: 'GW-01', name: 'BLE Gateway', kind: 'device', type: 'Gateway', icon: Bluetooth, tone: 'green', status: 'Active', x: 54, y: 16, zoneId: 'z-pz1', orientation: 0, lastUpdated: '2026-01-15T10:24:00', model: 'BG-500', serial: 'GW99812' },
  { id: 'd-ap01', code: 'AP-01', name: 'Wi-Fi AP', kind: 'device', type: 'Access Point', icon: Wifi, tone: 'blue', status: 'Active', x: 21, y: 16, orientation: 0, lastUpdated: '2026-01-15T10:24:00', model: 'AX-6', serial: 'AP11290' },
  { id: 'd-lg01', code: 'LG-01', name: 'LoRa Gateway', kind: 'device', type: 'Gateway', icon: Radio, tone: 'purple', status: 'Active', x: 42, y: 78, zoneId: 'z-mz1', orientation: 0, lastUpdated: '2026-01-14T09:00:00', model: 'LR-8', serial: 'LG55120' },
  { id: 'd-cam01', code: 'CAM-01', name: 'IP Camera', kind: 'device', type: 'Camera', icon: Cctv, tone: 'purple', status: 'Active', x: 41, y: 14, zoneId: 'z-pz1', orientation: 180, lastUpdated: '2026-01-15T08:12:00', model: 'IPC-4K', serial: 'CM00451' },
  { id: 'd-ts01', code: 'TS-01', name: 'Temp Sensor', kind: 'device', type: 'Sensor', icon: Thermometer, tone: 'green', status: 'Active', x: 13, y: 80, orientation: 0, lastUpdated: '2026-01-15T10:00:00', model: 'TH-2', serial: 'TS77001' },
  { id: 'd-em01', code: 'EM-01', name: 'Energy Meter', kind: 'device', type: 'Meter', icon: Zap, tone: 'orange', status: 'Active', x: 93, y: 40, zoneId: 'z-rz1', orientation: 0, lastUpdated: '2026-01-13T17:30:00', model: 'EM-3P', serial: 'EM40022' },
  { id: 'a-ch01', code: 'CH-001', name: 'Chiller-01', kind: 'asset', type: 'HVAC', assetType: 'Chiller', model: 'TR-350', serial: 'SN783452', icon: Snowflake, tone: 'blue', status: 'Active', x: 30, y: 45, zoneId: 'z-pz1', orientation: 0, lastUpdated: '2026-01-15T10:24:00' },
  { id: 'a-ahu01', code: 'AHU-01', name: 'AHU-01', kind: 'asset', type: 'HVAC', assetType: 'Air Handling Unit', model: 'AH-90', serial: 'SN771002', icon: Fan, tone: 'purple', status: 'Active', x: 49, y: 47, zoneId: 'z-pz1', orientation: 0, lastUpdated: '2026-01-15T10:24:00' },
  { id: 'a-pmp01', code: 'PMP-01', name: 'Pump-01', kind: 'asset', type: 'Mechanical', assetType: 'Pump', model: 'CP-40', serial: 'SN662310', icon: Gauge, tone: 'purple', status: 'Active', x: 73, y: 36, zoneId: 'z-sz1', orientation: 0, lastUpdated: '2026-01-12T12:00:00' },
  { id: 'a-ch03', code: 'CH-003', name: 'Chiller-03', kind: 'asset', type: 'HVAC', assetType: 'Chiller', model: 'TR-350', serial: 'SN783460', icon: Snowflake, tone: 'blue', status: 'Active', x: null, y: null, orientation: 0, lastUpdated: '2026-01-09T09:00:00' },
  { id: 'a-ahu02', code: 'AHU-002', name: 'AHU-02', kind: 'asset', type: 'HVAC', assetType: 'Air Handling Unit', model: 'AH-90', serial: 'SN771019', icon: Fan, tone: 'purple', status: 'Active', x: null, y: null, orientation: 0, lastUpdated: '2026-01-09T09:00:00' }
];

/* ===== Layers ===== */

export interface MapLayer {
  id: string;
  name: string;
  type: string;
  description: string;
  icon: LucideIconData;
  tone: Tone;
  enabled: boolean;
  visible: boolean;
  locked: boolean;
  showLabel: boolean;
  showId: boolean;
  showStatusIcon: boolean;
  useCategoryColor: boolean;
  labelPosition: string;
  categories: string[];
  statusFilter: string;
  opacity: number;
  color: string;
}

function layer(id: string, name: string, type: string, icon: LucideIconData, tone: Tone, enabled: boolean, description: string, extra: Partial<MapLayer> = {}): MapLayer {
  return {
    id, name, type, icon, tone, enabled, description, visible: true, locked: false, showLabel: true, showId: true, showStatusIcon: false,
    useCategoryColor: true, labelPosition: 'Bottom Center', categories: [], statusFilter: 'All Statuses', opacity: 80, color: '#5b2aa8', ...extra
  };
}

export const LAYERS: MapLayer[] = [
  layer('zones', 'Zones', 'Zone (Area)', Layers, 'purple', true, 'Operational zones drawn on the floor plan.'),
  layer('geofences', 'Geofences', 'Geofence (Alerting)', Layers, 'purple', true, 'Geofence boundaries that raise entry / exit alerts.'),
  layer('assets', 'Assets', 'Asset (Plotted / Tracked)', Package, 'purple', true, 'Displays assets and major equipment on the floor plan (e.g., chillers, AHUs, pumps, generators).', { categories: ['HVAC'] }),
  layer('devices', 'Devices', 'Device (IoT)', Wifi, 'blue', true, 'Gateways, access points, cameras, sensors and meters.'),
  layer('annotations', 'Annotations', 'Annotation (Text)', Type, 'grey', false, 'Free text notes and dimension markers.'),
  layer('rooms', 'Room & Space', 'Room (Space)', Building, 'orange', true, 'Rooms and spaces from the location hierarchy.'),
  layer('base', 'Floor Plan (Base)', 'Base Map', Layers, 'grey', true, 'The uploaded floor plan image.', { locked: true }),
  layer('heatmap', 'Heatmap (Utilization)', 'Heatmap', Warehouse, 'orange', false, 'Space utilization heatmap from occupancy sensors.')
];

export const LAYER_SETS = ['Operations View', 'Maintenance View', 'Security View', 'Audit View'];

/* ===== Lookups ===== */

export const TONE_HEX: Record<Tone, string> = {
  purple: '#7c3aed',
  blue: '#2563eb',
  pink: '#ec4899',
  green: '#10b981',
  orange: '#f59e0b',
  red: '#ef4444',
  grey: '#64748b'
};

export const STATUS_TONE: Record<MapStatus, Tone> = { Published: 'green', Draft: 'grey', Archived: 'orange' };

export function zoneIcon(type: ZoneType): LucideIconData {
  return ZONE_TYPES.find((t) => t.type === type)!.icon;
}

/** "15 Jan 2026" */
export function fmtDate(iso: string): string {
  return new Date(iso).toLocaleDateString('en-GB', { day: '2-digit', month: 'short', year: 'numeric' });
}

/** "10:24 AM" */
export function fmtTime(iso: string): string {
  return new Date(iso).toLocaleTimeString('en-US', { hour: '2-digit', minute: '2-digit' });
}
