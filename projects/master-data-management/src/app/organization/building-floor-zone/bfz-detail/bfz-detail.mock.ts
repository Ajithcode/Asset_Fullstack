import { BfzRecord } from '../bfz.model';

/*
 * DEMO MODE: sample rows for the Assets, Departments, People, Documents and Activity tabs.
 * Derived from the record code so each building always shows the same rows; the tab badges use
 * the building's real counts and the tables show the first few.
 */

function seed(code: string): number {
  return [...code].reduce((h, c) => (h * 31 + c.charCodeAt(0)) >>> 0, 17);
}

const ASSET_NAMES: [string, string][] = [
  ['CNC Machine', 'Machining'], ['Air Compressor', 'Utilities'], ['Conveyor Belt', 'Material Handling'],
  ['Chiller Unit', 'HVAC'], ['Forklift', 'Material Handling'], ['UPS 40kVA', 'Electrical'],
  ['Fire Pump', 'Safety'], ['Access Controller', 'Security']
];
const DEPARTMENTS = ['Production', 'Maintenance', 'Quality', 'Warehouse', 'Administration', 'HSE', 'IT'];
const PEOPLE: [string, string][] = [
  ['Rex Kumar', 'Plant Manager'], ['Aisha Rahman', 'Facility Manager'], ['Murugan S', 'Technician'],
  ['Divya Raman', 'Safety Officer'], ['Omar Haddad', 'Shift Supervisor'], ['Priya Nair', 'Store Keeper'],
  ['Karthik R', 'HVAC Technician'], ['Fatima Ali', 'Quality Engineer']
];

export function sampleAssets(r: BfzRecord) {
  const s = seed(r.code);
  return ASSET_NAMES.map(([name, category], i) => ({
    tag: `AST-${String(((s + i * 7919) % 90000) + 10000)}`,
    name: `${name} ${i + 1}`,
    category,
    status: ['In Use', 'In Use', 'Idle', 'Under Maintenance'][(s + i) % 4]
  }));
}

export function sampleDepartments(r: BfzRecord, count: number) {
  const s = seed(r.code);
  return Array.from({ length: Math.min(count, DEPARTMENTS.length) }, (_, i) => ({
    name: DEPARTMENTS[(s + i) % DEPARTMENTS.length],
    head: PEOPLE[(s + i) % PEOPLE.length][0],
    people: 8 + ((s + i * 13) % 60)
  }));
}

export function samplePeople(r: BfzRecord) {
  const s = seed(r.code);
  return PEOPLE.map(([name, role], i) => ({ name, role, department: DEPARTMENTS[(s + i) % DEPARTMENTS.length] }));
}

export function sampleDocuments(r: BfzRecord) {
  return [
    { name: `${r.name} - Floor Plans.pdf`, kind: 'Drawing', size: '6.2 MB', date: r.updatedOn.slice(0, 10) },
    { name: `${r.name} - Fire Safety Certificate.pdf`, kind: 'Certificate', size: '420 KB', date: r.updatedOn.slice(0, 10) },
    { name: `${r.name} - Lease Agreement.pdf`, kind: 'Contract', size: '1.1 MB', date: r.createdOn.slice(0, 10) }
  ];
}

export function sampleActivity(r: BfzRecord) {
  const list = [
    { when: r.updatedOn, who: 'Rex Kumar', text: `Updated ${r.type.toLowerCase()} details` },
    { when: r.createdOn, who: 'Aisha Rahman', text: `Created ${r.type.toLowerCase()} ${r.code}` }
  ];
  if (r.status === 'Inactive') list.unshift({ when: r.updatedOn, who: 'Rex Kumar', text: `Marked ${r.type.toLowerCase()} as Inactive` });
  return list;
}
