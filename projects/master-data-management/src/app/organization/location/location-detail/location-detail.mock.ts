import { LocationRecord } from '../location.model';

/*
 * DEMO MODE: per-location content for the detail tabs (assets, people, documents, activity).
 * Values are derived from the location code so each location always shows the same data.
 */

export interface LocationAsset {
  tag: string;
  name: string;
  category: string;
  condition: string;
  status: 'In Use' | 'Idle' | 'Under Maintenance';
}

export interface LocationPerson {
  name: string;
  role: string;
  shift: string;
  phone: string;
}

export interface LocationDocument {
  name: string;
  kind: string;
  size: string;
  uploadedOn: string;
  uploadedBy: string;
}

export interface LocationActivity {
  when: string;
  who: string;
  text: string;
}

function seed(code: string): number {
  return [...code].reduce((h, c) => (h * 31 + c.charCodeAt(0)) >>> 0, 11);
}

const ASSETS_BY_TYPE: Record<string, [string, string][]> = {
  Area: [['CNC Machine', 'Machining'], ['Conveyor Belt', 'Material Handling'], ['Air Compressor', 'Utilities'], ['Fire Extinguisher', 'Safety'], ['Industrial Fan', 'HVAC']],
  Room: [['Split AC', 'HVAC'], ['Projector', 'IT'], ['Smoke Detector', 'Safety'], ['Access Reader', 'Security'], ['Workstation PC', 'IT']],
  Rack: [['Tool Kit', 'Tools'], ['Torque Wrench', 'Tools'], ['Fixture Set', 'Tools']],
  Bin: [['Spare Bearings', 'Spares'], ['V-Belts', 'Spares']],
  Workstation: [['Desktop PC', 'IT'], ['Monitor 27"', 'IT'], ['IP Phone', 'IT']],
  Equipment: [['Spindle Motor', 'Machining'], ['Coolant Pump', 'Machining'], ['Control Panel', 'Electrical']],
  'Parking Bay': [['EV Charger', 'Electrical'], ['CCTV Camera', 'Security']],
  Yard: [['Diesel Generator', 'Power'], ['Fuel Tank', 'Power'], ['Flood Light', 'Electrical']],
  Dock: [['Dock Leveller', 'Material Handling'], ['Forklift', 'Material Handling'], ['Dock Shelter', 'Material Handling']]
};

const PEOPLE: [string, string][] = [
  ['Rex Kumar', 'Location Owner'],
  ['Aisha Rahman', 'Facility Manager'],
  ['Murugan S', 'Maintenance Technician'],
  ['Divya Raman', 'Safety Officer'],
  ['Omar Haddad', 'Shift Supervisor'],
  ['Priya Nair', 'Store Keeper']
];

export function assetsFor(r: LocationRecord): LocationAsset[] {
  const s = seed(r.code);
  const pool = ASSETS_BY_TYPE[r.type] ?? ASSETS_BY_TYPE['Area'];
  const statuses: LocationAsset['status'][] = ['In Use', 'In Use', 'Idle', 'Under Maintenance'];
  const conditions = ['Excellent', 'Good', 'Good', 'Fair'];
  return pool.map(([name, category], i) => ({
    tag: `AST-${String((s + i * 97) % 90000 + 10000)}`,
    name: `${name} ${i + 1}`,
    category,
    condition: conditions[(s + i) % conditions.length],
    status: r.status === 'Inactive' ? 'Idle' : statuses[(s + i) % statuses.length]
  }));
}

export function peopleFor(r: LocationRecord): LocationPerson[] {
  const s = seed(r.code);
  const count = 2 + (s % 3);
  return Array.from({ length: count }, (_, i) => {
    const [name, role] = PEOPLE[(s + i) % PEOPLE.length];
    return {
      name,
      role: i === 0 ? 'Location Owner' : role,
      shift: ['General (09:00-18:00)', 'Morning (06:00-14:00)', 'Night (22:00-06:00)'][(s + i) % 3],
      phone: `+971 50 ${String((s + i * 7919) % 900 + 100)} ${String((s + i * 104729) % 9000 + 1000)}`
    };
  });
}

export function documentsFor(r: LocationRecord): LocationDocument[] {
  const date = r.updatedOn.slice(0, 10);
  return [
    { name: `${r.name} - Layout.pdf`, kind: 'Floor Layout', size: '2.4 MB', uploadedOn: date, uploadedBy: 'Rex Kumar' },
    { name: `${r.code} Safety Inspection.pdf`, kind: 'Inspection Report', size: '860 KB', uploadedOn: date, uploadedBy: 'Divya Raman' },
    { name: `${r.code} Photos.zip`, kind: 'Photos', size: '14.1 MB', uploadedOn: r.createdOn.slice(0, 10), uploadedBy: 'Aisha Rahman' }
  ];
}

export function activityFor(r: LocationRecord): LocationActivity[] {
  const list: LocationActivity[] = [
    { when: r.updatedOn, who: 'Rex Kumar', text: 'Updated location details' },
    { when: r.createdOn, who: 'Aisha Rahman', text: `Created location ${r.code}` }
  ];
  if (r.status === 'Inactive') {
    list.unshift({ when: r.updatedOn, who: 'Rex Kumar', text: 'Marked location as Inactive' });
  }
  return list;
}
