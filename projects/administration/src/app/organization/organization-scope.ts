import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable, catchError, forkJoin, map, of } from 'rxjs';
import { environment } from 'shared-ui';
import { OrgLocation, OrgTabId, RecordStatus, SummaryRow } from './organization.model';

/**
 * What Administration > Organization shows for one organization, read from the Master Data and Asset APIs.
 *
 * The chain that ties records to an organization is: Organization <- Business Unit <- Site / Department <- Employee,
 * and an asset belongs to it through its site or department. Vendors, maintenance plans, inventory items and
 * compliance records have no organization link in the APIs, so those tabs list the whole client's records.
 */

export interface OrgOption {
  id: string;
  name: string;
  code: string;
}

/** connected = the API answered; broken = the call failed (the tab shows no rows). */
export type SourceState = 'connected' | 'broken';

export interface TabSource {
  /** Endpoints behind the tab, for the note under the tab bar. */
  apis: string[];
  state: SourceState;
  /** organization = filtered to the selected organization; client = not linked to an organization, all records. */
  scope: 'organization' | 'client';
}

export interface OrgScope {
  locations: OrgLocation[];
  rows: Record<Exclude<OrgTabId, 'location'>, SummaryRow[]>;
  sources: Record<OrgTabId, TabSource>;
}

interface Ref {
  id?: string | null;
  name?: string | null;
}

/** Fields the master data responses share; each resource adds its own. */
type Rec = Record<string, any> & { id?: string; code?: string | null; status?: string | null; isDeleted?: boolean };

const MDM = `${environment.masterDataApiUrl}api/master-data/`;

const ACTIVE = (r: Rec) => r.isDeleted !== true;
const status = (r: Rec): RecordStatus => (/^(inactive|disabled|retired|disposed|deleted)$/i.test(r.status ?? '') ? 'Inactive' : 'Active');
const text = (v: unknown): string => (typeof v === 'string' ? v : v === null || v === undefined ? '' : String(v));
const key = (v: unknown) => text(v).trim().toLowerCase();

/** Organizations for the picker (GET /api/master-data/organizations); errors surface to the caller. */
export function loadOrganizations(http: HttpClient): Observable<OrgOption[]> {
  return http.get<Rec[]>(`${MDM}organizations`, { params: new HttpParams().set('clientId', environment.clientId) }).pipe(
    map((list) =>
      (Array.isArray(list) ? list : [])
        .filter((o) => ACTIVE(o) && o.id && status(o) === 'Active')
        .map((o) => ({ id: String(o.id), name: text(o['organizationName'] ?? o.code), code: text(o.code) }))
        .sort((a, b) => a.name.localeCompare(b.name))
    )
  );
}

/** One list call; a failure becomes ok:false instead of failing the whole load. */
function fetch(http: HttpClient, url: string, params: Record<string, string | number> = {}): Observable<{ ok: boolean; rows: Rec[] }> {
  let p = new HttpParams().set('clientId', environment.clientId);
  for (const [k, v] of Object.entries(params)) p = p.set(k, String(v));
  return http.get<Rec[] | { items?: Rec[] }>(url, { params: p }).pipe(
    map((res) => ({ ok: true, rows: (Array.isArray(res) ? res : res?.items ?? []).filter(ACTIVE) })),
    catchError(() => of({ ok: false, rows: [] as Rec[] }))
  );
}

const refIn = (ref: Ref | null | undefined, ids: Set<string>) => !!ref?.id && ids.has(ref.id);

export function loadOrganizationScope(http: HttpClient, org: OrgOption): Observable<OrgScope> {
  const md = (resource: string) => fetch(http, MDM + resource);
  return forkJoin({
    businessUnits: md('business-units'),
    sites: md('sites'),
    buildings: md('buildings'),
    floors: md('floors'),
    zones: md('zones'),
    departments: md('departments'),
    employees: md('employees'),
    assets: fetch(http, `${environment.assetApiUrl}api/asset-registrations`, { tenantId: environment.tenantId, page: 1, pageSize: 1000 }),
    suppliers: md('suppliers'),
    serviceProviders: md('service-providers'),
    amcVendors: md('amc-vendors'),
    taskTemplates: md('task-templates'),
    items: md('items'),
    regulations: md('regulations'),
    standards: md('standards'),
    permitTypes: md('permit-types'),
    certificationTypes: md('certification-types')
  }).pipe(map((d) => build(org, d)));
}

type Loaded = Record<string, { ok: boolean; rows: Rec[] }>;

function build(org: OrgOption, d: Loaded): OrgScope {
  // Organization -> business units -> sites / departments -> employees
  const bus = d['businessUnits'].rows.filter((b) => b['parentOrganization']?.id === org.id);
  const buIds = new Set(bus.map((b) => String(b.id)));
  const sites = d['sites'].rows.filter((s) => refIn(s['businessUnit'], buIds));
  const siteIds = new Set(sites.map((s) => String(s.id)));
  const departments = d['departments'].rows.filter((x) => refIn(x['businessUnit'], buIds));
  const deptIds = new Set(departments.map((x) => String(x.id)));
  const employees = d['employees'].rows.filter((e) => refIn(e['department'], deptIds));
  const siteNames = new Set(sites.map((s) => key(s['siteName'])));
  const deptNames = new Set(departments.map((x) => key(x['departmentName'])));
  const assets = d['assets'].rows.filter((a) => siteNames.has(key(a['siteName'])) || deptNames.has(key(a['department'])));

  // Location hierarchy: site (Location) > building > floor > zone, linked by id, shown by code
  const buildings = d['buildings'].rows.filter((b) => refIn(b['site'], siteIds));
  const buildingIds = new Set(buildings.map((b) => String(b.id)));
  const floors = d['floors'].rows.filter((f) => buildingIds.has(text(f['buildingId'])));
  const floorIds = new Set(floors.map((f) => String(f.id)));
  const zones = d['zones'].rows.filter((z) => floorIds.has(text(z['floorId'])) || buildingIds.has(text(z['buildingId'])));
  const codeOf = new Map<string, string>();
  for (const r of [...sites, ...buildings, ...floors, ...zones]) codeOf.set(String(r.id), text(r.code));
  const location = (r: Rec, name: string, type: OrgLocation['type'], parent: string | null): OrgLocation => ({
    id: String(r.id),
    code: text(r.code),
    name,
    type,
    parent,
    address: '',
    country: text(r['country']),
    city: text(r['city']),
    timeZone: '',
    contact: '',
    status: status(r)
  });
  const locations: OrgLocation[] = [
    ...sites.map((s) => location(s, text(s['siteName']), 'Location', null)),
    ...buildings.map((b) => location(b, text(b['name']), 'Building', codeOf.get(text(b['site']?.id)) ?? null)),
    ...floors.map((f) => location(f, text(f['name']), 'Floor', codeOf.get(text(f['buildingId'])) ?? null)),
    ...zones.map((z) => location(z, text(z['name']), 'Zone', codeOf.get(text(z['floorId'])) ?? codeOf.get(text(z['buildingId'])) ?? null))
  ];

  const vendorRow = (r: Rec, name: string, type: string, services: string): SummaryRow => ({
    code: text(r.code), name, type, services, contact: text(r['primaryContact']), sla: '', rating: '', status: status(r)
  });
  const itemStock = (r: Rec): string => {
    const onHand = Number(r['currentStock'] ?? 0);
    const reorder = Number(r['reorderLevel'] ?? 0);
    return onHand <= 0 ? 'Out of Stock' : onHand <= reorder ? 'Low Stock' : 'In Stock';
  };
  const rule = (kind: string, r: Rec, name: string, extra: SummaryRow = {}): SummaryRow => ({
    code: text(r.code), kind, regulation: '', standard: '', certificate: '', permit: '', expiry: '', validity: '', owner: '', reminder: '',
    ...extra, name, status: status(r)
  });

  const ok = (...names: string[]) => (names.every((n) => d[n].ok) ? 'connected' : 'broken') as SourceState;
  const mdm = (r: string) => `GET /api/master-data/${r}`;

  return {
    locations,
    rows: {
      asset: assets.map((a) => ({
        code: text(a['assetTag'] || a['assetRegistrationId']),
        name: text(a['assetName']),
        category: text(a['category']),
        model: text(a['assetType']),
        serial: '',
        location: text(a['locationPath'] || a['siteName']),
        custodian: text(a['custodianName']),
        criticality: text(a['criticality']),
        lifecycle: text(a['status']),
        status: status({ status: /inactive|retired|disposed/i.test(text(a['status'])) ? 'Inactive' : 'Active' })
      })),
      people: employees.map((e) => ({
        code: text(e['employeeId']),
        name: text(e['employeeName']),
        department: text(e['department']?.name),
        role: text(e['designation']),
        skill: '',
        shift: '',
        assets: '',
        status: status(e)
      })),
      vendor: [
        ...d['suppliers'].rows.map((r) => vendorRow(r, text(r['supplierName']), 'Supplier', text(r['category']))),
        ...d['serviceProviders'].rows.map((r) => vendorRow(r, text(r['serviceProviderName']), 'Service Contractor', text(r['serviceType']))),
        ...d['amcVendors'].rows.map((r) => vendorRow(r, text(r['amcVendorName']), 'Service Contractor', text(r['contractType']) || 'AMC'))
      ],
      maintenance: d['taskTemplates'].rows.map((r) => ({
        code: text(r.code),
        name: text(r['templateName']),
        type: text(r['maintenanceType']?.name),
        work: '',
        priority: '',
        frequency: '',
        checklist: r['taskCount'] ? `${r['taskCount']} tasks` : '',
        sla: '',
        escalation: '',
        status: status(r)
      })),
      inventory: d['items'].rows.map((r) => ({
        code: text(r['itemCode']),
        item: text(r['itemName']),
        category: text(r['category']?.name),
        uom: text(r['uom']?.name),
        levels: '',
        bin: '',
        onHand: text(r['currentStock']),
        stock: itemStock(r),
        reorder: text(r['reorderLevel']),
        status: status(r)
      })),
      compliance: [
        ...d['regulations'].rows.map((r) => rule('Regulation', r, text(r['regulationName']), { regulation: text(r['regulationName']), owner: text(r['issuingAuthority']) })),
        ...d['standards'].rows.map((r) => rule('Standard', r, text(r['standardName']), { standard: text(r['standardName']), owner: text(r['organization']) })),
        ...d['permitTypes'].rows.map((r) => rule('Permit Type', r, text(r['permitTypeName'] ?? r['name']), { permit: text(r['permitTypeName'] ?? r['name']) })),
        ...d['certificationTypes'].rows.map((r) => rule('Certificate', r, text(r['certificationTypeName'] ?? r['name']), { certificate: text(r['certificationTypeName'] ?? r['name']) }))
      ]
    },
    sources: {
      location: {
        apis: [mdm('sites'), mdm('buildings'), mdm('floors'), mdm('zones'), mdm('business-units')],
        state: ok('businessUnits', 'sites', 'buildings', 'floors', 'zones'),
        scope: 'organization'
      },
      asset: {
        apis: ['GET /api/asset-registrations', mdm('sites'), mdm('departments'), mdm('business-units')],
        state: ok('assets', 'sites', 'departments', 'businessUnits'),
        scope: 'organization'
      },
      people: {
        apis: [mdm('employees'), mdm('departments'), mdm('business-units')],
        state: ok('employees', 'departments', 'businessUnits'),
        scope: 'organization'
      },
      vendor: { apis: [mdm('suppliers'), mdm('service-providers'), mdm('amc-vendors')], state: ok('suppliers', 'serviceProviders', 'amcVendors'), scope: 'client' },
      maintenance: { apis: [mdm('task-templates')], state: ok('taskTemplates'), scope: 'client' },
      inventory: { apis: [mdm('items')], state: ok('items'), scope: 'client' },
      compliance: {
        apis: [mdm('regulations'), mdm('standards'), mdm('permit-types'), mdm('certification-types')],
        state: ok('regulations', 'standards', 'permitTypes', 'certificationTypes'),
        scope: 'client'
      }
    }
  };
}
