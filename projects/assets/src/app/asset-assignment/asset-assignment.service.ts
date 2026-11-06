import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, catchError, concat, expand, forkJoin, map, of, reduce, shareReplay, takeWhile, toArray } from 'rxjs';
import { AuthService, environment } from 'shared-ui';
import { ASSET_API, TENANT, MEDIA_API, EVENTLOG_API } from '../services/asset-api';
import {
  Assignment, AssignmentEvent, AssignmentStatus, AssignmentType, DepartmentManager, Employee, PoolAsset, SiteNode, isoDate, kindOf, stamp,
} from './asset-assignment-data';

// ===== Asset API contracts (Contracts/AssetAssignmentContracts.cs, camelCase) =====

export interface AssignmentAssetDto {
  assetRegistrationId: string;
  assetName: string | null;
  assetTag: string | null;
  serialNumber: string | null;
  model: string | null;
  manufacturer: string | null;
  category: string | null;
  assetType: string | null;
  currentLocation: string | null;
  previousStatus: string | null;
}

export interface AssignmentPersonDto {
  id: string | null;
  code: string | null;
  name: string | null;
  designation: string | null;
}

export interface AssignmentEmployeeDto {
  employeeId: string;
  employeeCode: string | null;
  employeeName: string | null;
  designation: string | null;
  departmentId: string | null;
  departmentName: string | null;
  manager: AssignmentPersonDto | null;
}

export interface AssignmentDepartmentDto {
  departmentId: string;
  departmentName: string | null;
  manager: AssignmentPersonDto | null;
}

export interface AssignmentOtherEntityDto {
  entityType: string;
  entityId: string | null;
  entityName: string;
  contactPerson: string | null;
  contactPhone: string | null;
  contactEmail: string | null;
  agreementReference: string | null;
  reason: string | null;
  serviceType: string | null;
  serviceRequestNumber: string | null;
  serviceLocation: string | null;
  expectedReturnDate: string | null;
  returnCondition: string | null;
  responsiblePerson: AssignmentPersonDto | null;
}

export interface AssignmentLocationDto {
  siteId: string | null;
  siteCode: string | null;
  siteName: string | null;
  buildingId: string | null;
  buildingName: string | null;
  floorId: string | null;
  floorName: string | null;
  roomId: string | null;
  roomName: string | null;
  /** Response only, built on the server. */
  locationPath?: string | null;
}

export interface AssignmentAttachmentDto {
  fileName: string;
  fileUrl: string;
  contentType: string | null;
  fileSize: number | null;
}

export interface AssignmentNotificationDto {
  notifyAssignees: boolean;
  message: string | null;
  notifyManager: boolean;
  notifyDepartment: boolean;
}

export interface AssignmentReminderDto {
  isEnabled: boolean;
  reminderDate: string | null;
  message: string | null;
}

export interface AssignmentCustomFieldsDto {
  assignmentReference: string | null;
  requestorId: string | null;
  requestorName: string | null;
  priority: string | null;
  assignmentReason: string | null;
}

/** AssetAssignmentResponse. */
export interface AssetAssignmentDto {
  id: string;
  assignmentNumber: string;
  /** "Draft", "Active" or "Returned". */
  status: string | null;
  selectionMode: string | null;
  bulkAssignmentId: string | null;
  assetCount: number;
  assets: AssignmentAssetDto[];
  /** "Employee", "Department", "Location" or "Other Entity". */
  assignTo: string;
  assignedToName: string | null;
  assignmentType: string | null;
  fromDate: string | null;
  toDate: string | null;
  isOpenEnded: boolean;
  employee: AssignmentEmployeeDto | null;
  department: AssignmentDepartmentDto | null;
  otherEntity: AssignmentOtherEntityDto | null;
  location: AssignmentLocationDto | null;
  costCenter: string | null;
  purpose: string | null;
  approvalReference: string | null;
  remarks: string | null;
  notes: string | null;
  attachments: AssignmentAttachmentDto[];
  notification: AssignmentNotificationDto;
  reminder: AssignmentReminderDto;
  customFields: AssignmentCustomFieldsDto;
  createdBy: string | null;
  createdAt: string | null;
  updatedBy: string | null;
  updatedAt: string | null;
  clientId: string | null;
  tenantId: string | null;
}

interface AssetAssignmentListDto {
  items: AssetAssignmentDto[];
  totalCount: number;
  statusCounts: Record<string, number>;
  page: number;
  pageSize: number;
}

/** CreateAssetAssignmentRequest (POST and PUT). */
export interface AssetAssignmentRequest {
  selectionMode: string | null;
  assetRegistrationIds: string[];
  assignTo: string | null;
  assignmentType: string | null;
  fromDate: string | null;
  toDate: string | null;
  isOpenEnded: boolean;
  employee: AssignmentEmployeeDto | null;
  department: AssignmentDepartmentDto | null;
  otherEntity: AssignmentOtherEntityDto | null;
  location: AssignmentLocationDto | null;
  costCenter: string | null;
  purpose: string | null;
  approvalReference: string | null;
  remarks: string | null;
  notes: string | null;
  attachments: AssignmentAttachmentDto[];
  notification: AssignmentNotificationDto | null;
  reminder: AssignmentReminderDto | null;
  customFields: AssignmentCustomFieldsDto | null;
  createdBy: string | null;
  clientId: string | null;
  tenantId: string | null;
}

/** The part of a request that says who / where the assets go. */
export type AssignmentTarget = Pick<AssetAssignmentRequest, 'assignTo' | 'employee' | 'department' | 'otherEntity' | 'location'>;

interface EventLogDto {
  id: string;
  entityName: string | null;
  action: string;
  performedBy: string | null;
  createdAt: string | null;
  message: string | null;
}

/** AssetListItemResponse (GET /api/asset-registrations, read only). */
interface AssetListItemDto {
  id: string;
  assetRegistrationId: string;
  assetName: string | null;
  assetTag: string | null;
  category: string | null;
  assetType: string | null;
  siteName: string | null;
  buildingName: string | null;
  floorName: string | null;
  roomName: string | null;
  locationPath: string | null;
  custodianName: string | null;
  department: string | null;
  status: string | null;
  manufacturer: string | null;
}

interface AssetListDto {
  items: AssetListItemDto[];
  totalCount: number;
}

// ===== Master data (MasterDataManagement /api/master-data/*, read only) =====

interface MdmRef {
  id: string | null;
  name: string | null;
}

interface MdmEmployeeDto {
  id: string;
  employeeName: string;
  employeeId: string;
  department: MdmRef | null;
  designation: string | null;
  site: string | null;
  costCenter: string | null;
  reportingManager: MdmRef | null;
  status: string | null;
}

interface MdmDepartmentDto {
  id: string;
  departmentName: string;
  code: string;
  departmentHead: MdmRef | null;
  status: string | null;
}

interface MdmSiteDto {
  id: string;
  siteName: string;
  code: string;
  siteType: string | null;
  city: string | null;
  country: string | null;
  address: { line1: string | null; line2: string | null; city: string | null; state: string | null; country: string | null } | null;
  status: string | null;
}

interface MdmBuildingDto {
  id: string;
  name: string;
  code: string;
  site: MdmRef | null;
}

interface MdmFloorDto {
  id: string;
  name: string;
  code: string;
  site: MdmRef | null;
  buildingId: string;
  buildingName: string;
}

interface MdmLocationDto {
  id: string;
  locationName: string;
  code: string;
  type: string;
  building: string | null;
  floor: string | null;
  site: MdmRef | null;
}

/** An employee from master data, in the shape the pickers show. */
export interface LookupEmployee extends Employee {
  mdmId: string;
  departmentId: string;
  costCenter: string;
  managerId: string;
  managerName: string;
  active: boolean;
}

export interface LookupDepartment {
  id: string;
  name: string;
  code: string;
  manager?: DepartmentManager & { mdmId: string };
  active: boolean;
}

export interface LookupSite {
  id: string;
  name: string;
  code: string;
  type: string;
  city: string;
  country: string;
  address: string;
}

export interface LookupPlace {
  id: string;
  name: string;
  code: string;
  siteId: string;
  /** Floors: their building; rooms: building name. */
  buildingId: string;
  buildingName: string;
  /** Rooms: floor name. */
  floorName: string;
}

/** Employees, departments and places for the assignee pickers. */
export interface AssignmentLookups {
  employees: LookupEmployee[];
  departments: LookupDepartment[];
  sites: LookupSite[];
  buildings: LookupPlace[];
  floors: LookupPlace[];
  rooms: LookupPlace[];
}

export const EMPTY_LOOKUPS: AssignmentLookups = { employees: [], departments: [], sites: [], buildings: [], floors: [], rooms: [] };

/** Default Assignment Type for each kind of assignee (the wizard's first choice). */
export const DEFAULT_ASSIGNMENT_TYPE: Record<AssignmentType, string> = {
  Employee: 'Primary User',
  Department: 'Department Use',
  Location: 'Primary Location',
  Other: 'Temporary Transfer',
};

const ASSIGN_TO: Record<AssignmentType, string> = { Employee: 'Employee', Department: 'Department', Location: 'Location', Other: 'Other Entity' };

export function assignToOf(type: AssignmentType): string {
  return ASSIGN_TO[type];
}

export function typeOf(assignTo: string | null | undefined): AssignmentType {
  const t = (assignTo ?? '').toLowerCase();
  if (t === 'employee') return 'Employee';
  if (t === 'department') return 'Department';
  if (t === 'location') return 'Location';
  return 'Other';
}

/** "2025-01-01T00:00:00Z" → "2025-01-01". */
export function dayOf(value: string | null | undefined): string {
  return value ? value.slice(0, 10) : '';
}

const blank = (s: string | null | undefined) => (s && s.trim() ? s.trim() : null);
const same = (a: string | null | undefined, b: string | null | undefined) => !!a && !!b && a.trim().toLowerCase() === b.trim().toLowerCase();

/** Last level of a location ("Head Office > Block A > IT-204" → "IT-204"). */
function placeName(l: AssignmentLocationDto | null): string {
  return l?.roomName || l?.floorName || l?.buildingName || l?.siteName || l?.locationPath || '';
}

function isActiveStatus(status: string | null | undefined): boolean {
  return !status || /^(active|enabled)$/i.test(status);
}

/** UI status of an assignment: Returned, Overdue (past its to date) or Assigned. */
export function statusOf(dto: AssetAssignmentDto): AssignmentStatus {
  if (/^returned$/i.test(dto.status ?? '')) return 'Returned';
  const to = dayOf(dto.toDate);
  return to && to < isoDate(new Date()) ? 'Overdue' : 'Assigned';
}

/** One list row per asset in the assignment. */
export function toRows(dto: AssetAssignmentDto): Assignment[] {
  const type = typeOf(dto.assignTo);
  let assignee = dto.assignedToName ?? '';
  let assigneeSub = '';
  if (type === 'Employee') {
    assignee = dto.employee?.employeeName || dto.employee?.employeeCode || assignee;
    assigneeSub = dto.employee?.designation ?? '';
  } else if (type === 'Department') {
    assignee = dto.department?.departmentName || assignee;
    assigneeSub = 'Department';
  } else if (type === 'Location') {
    assignee = placeName(dto.location) || assignee;
    assigneeSub = dto.location?.siteName && dto.location.siteName !== assignee ? dto.location.siteName : 'Location/Site';
  } else {
    assignee = dto.otherEntity?.entityName || assignee;
    assigneeSub = dto.otherEntity?.entityType ?? 'Other Entity';
  }
  const location = dto.location?.locationPath?.replace(/ > /g, ' - ') || dto.otherEntity?.serviceLocation || '';
  const created = dto.createdAt ? stamp(new Date(dto.createdAt)) : '';
  const extra = [
    { label: 'Assignment No.', value: dto.assignmentNumber },
    { label: 'Assignment Type', value: dto.assignmentType ?? '' },
    { label: 'Purpose', value: dto.purpose ?? '' },
    { label: 'Approval Reference', value: dto.approvalReference ?? '' },
    { label: 'Cost Center', value: dto.costCenter ?? '' },
    { label: 'Reference', value: dto.customFields?.assignmentReference ?? '' },
    { label: 'Requestor', value: dto.customFields?.requestorName ?? '' },
    { label: 'Priority', value: dto.customFields?.priority ?? '' },
    { label: 'Assignment Reason', value: dto.customFields?.assignmentReason ?? '' },
    { label: 'Contact Person', value: dto.otherEntity?.contactPerson ?? '' },
    { label: 'Agreement / PO', value: dto.otherEntity?.agreementReference ?? '' },
    { label: 'Service Request', value: dto.otherEntity?.serviceRequestNumber ?? '' },
    { label: 'Responsible', value: dto.otherEntity?.responsiblePerson?.name ?? '' },
    { label: 'Documents', value: (dto.attachments ?? []).map((a) => a.fileName).join(', ') },
    { label: 'Notes', value: dto.notes ?? '' },
  ].filter((x) => x.value);
  return (dto.assets ?? []).map((a) => ({
    id: `${dto.id}|${a.assetRegistrationId}`,
    assignmentId: dto.id,
    assignmentNumber: dto.assignmentNumber,
    assetRegistrationId: a.assetRegistrationId,
    assetName: a.assetName || a.assetRegistrationId,
    assetType: a.assetType ?? '',
    kind: kindOf(`${a.assetType ?? ''} ${a.category ?? ''} ${a.assetName ?? ''}`),
    tag: a.assetTag || a.assetRegistrationId,
    serial: a.serialNumber ?? '',
    type,
    assignee,
    assigneeSub,
    department: dto.employee?.departmentName || dto.department?.departmentName || '',
    location,
    from: dayOf(dto.fromDate),
    to: dayOf(dto.toDate),
    status: statusOf(dto),
    assignedBy: dto.createdBy ?? '',
    assignedByRole: dto.selectionMode === 'Bulk Upload' ? 'Bulk Upload' : '',
    assignedOn: created,
    remarks: dto.remarks ?? '',
    extra,
    history: [],
  }));
}

/** Request that saves the assignment back as it is; callers then change what they need. */
export function requestFrom(dto: AssetAssignmentDto): AssetAssignmentRequest {
  const type = typeOf(dto.assignTo);
  const mode = dto.selectionMode && ['single', 'multiple', 'scan'].includes(dto.selectionMode.toLowerCase()) ? dto.selectionMode : null;
  const location = dto.location ? { ...dto.location } : null;
  if (location) delete location.locationPath;
  return {
    selectionMode: mode,
    assetRegistrationIds: (dto.assets ?? []).map((a) => a.assetRegistrationId),
    assignTo: dto.assignTo,
    assignmentType: dto.assignmentType || DEFAULT_ASSIGNMENT_TYPE[type],
    fromDate: dayOf(dto.fromDate) || null,
    toDate: dto.isOpenEnded ? null : dayOf(dto.toDate) || null,
    isOpenEnded: dto.isOpenEnded || !dto.toDate,
    employee: dto.employee,
    department: dto.department,
    otherEntity: dto.otherEntity
      ? { ...dto.otherEntity, responsiblePerson: dto.otherEntity.responsiblePerson ?? { id: null, code: null, name: dto.otherEntity.contactPerson || dto.otherEntity.entityName, designation: null } }
      : null,
    location,
    costCenter: dto.costCenter,
    purpose: dto.purpose,
    approvalReference: dto.approvalReference,
    remarks: dto.remarks,
    notes: dto.notes,
    attachments: dto.attachments ?? [],
    notification: dto.notification,
    reminder: dto.reminder?.isEnabled && !dto.reminder.reminderDate ? { ...dto.reminder, isEnabled: false } : dto.reminder,
    customFields: dto.customFields,
    createdBy: dto.createdBy,
    clientId: dto.clientId ?? TENANT.clientId,
    tenantId: dto.tenantId ?? TENANT.tenantId,
  };
}

/** Site, building, floor and room names → the location section of a request (ids looked up in master data). */
export function locationFrom(lk: AssignmentLookups, names: { site?: string; building?: string; floor?: string; room?: string }): AssignmentLocationDto | null {
  const site = lk.sites.find((s) => same(s.name, names.site));
  const building = lk.buildings.find((b) => same(b.name, names.building) && (!site || !b.siteId || b.siteId === site.id));
  const floor = lk.floors.find((f) => same(f.name, names.floor) && (!building || !f.buildingId || f.buildingId === building.id));
  const room = lk.rooms.find((r) => same(r.name, names.room) && (!site || !r.siteId || r.siteId === site.id));
  const result: AssignmentLocationDto = {
    siteId: site?.id ?? null,
    siteCode: site?.code ?? null,
    siteName: site?.name ?? blank(names.site),
    buildingId: building?.id ?? null,
    buildingName: building?.name ?? blank(names.building),
    floorId: floor?.id ?? null,
    floorName: floor?.name ?? blank(names.floor),
    roomId: room?.id ?? null,
    roomName: room?.name ?? blank(names.room),
  };
  return result.siteId || result.siteName || result.buildingName || result.floorName || result.roomName ? result : null;
}

export function employeeRequest(e: LookupEmployee | undefined, name: string): AssignmentEmployeeDto {
  return {
    employeeId: e?.mdmId ?? '',
    employeeCode: e?.id ?? null,
    employeeName: e?.name ?? blank(name),
    designation: e?.title || null,
    departmentId: e?.departmentId || null,
    departmentName: e?.department || null,
    manager: e?.managerId || e?.managerName ? { id: e.managerId || null, code: null, name: e.managerName || null, designation: null } : null,
  };
}

export function departmentRequest(d: LookupDepartment | undefined, name: string): AssignmentDepartmentDto {
  const m = d?.manager;
  return {
    departmentId: d?.id ?? '',
    departmentName: d?.name ?? blank(name),
    manager: m ? { id: m.mdmId || null, code: m.id || null, name: m.name, designation: m.title || null } : null,
  };
}

/** Who / where for a simple pick by name (list Reassign): employee, department, site or entity name. */
export function targetFor(lk: AssignmentLookups, type: AssignmentType, name: string, entityKind?: string): AssignmentTarget {
  const target: AssignmentTarget = { assignTo: ASSIGN_TO[type], employee: null, department: null, otherEntity: null, location: null };
  if (type === 'Employee') {
    const e = lk.employees.find((x) => x.name === name);
    target.employee = employeeRequest(e, name);
    if (e?.location) target.location = locationFrom(lk, { site: e.location });
  } else if (type === 'Department') {
    target.department = departmentRequest(lk.departments.find((x) => x.name === name), name);
  } else if (type === 'Location') {
    const [site, ...rest] = name.split(' - ');
    target.location = locationFrom(lk, { site, building: rest.join(' - ') || undefined });
  } else {
    target.otherEntity = {
      entityType: entityKind ?? 'Other', entityId: null, entityName: name, contactPerson: null, contactPhone: null, contactEmail: null, agreementReference: null,
      reason: null, serviceType: null, serviceRequestNumber: null, serviceLocation: null, expectedReturnDate: null, returnCondition: null,
      responsiblePerson: { id: null, code: null, name, designation: null },
    };
  }
  return target;
}

/** Names offered in the assignee pickers for a kind of assignee. */
export function choicesFor(lk: AssignmentLookups, type: AssignmentType, entityNames: string[]): string[] {
  if (type === 'Employee') return lk.employees.map((e) => e.name);
  if (type === 'Department') return lk.departments.map((d) => d.name);
  if (type === 'Location') return lk.sites.map((s) => s.name);
  return entityNames;
}

/** Location / site tree: country → city → site → buildings. */
export function siteTreeOf(lk: AssignmentLookups): SiteNode[] {
  const countries = new Map<string, SiteNode>();
  for (const s of lk.sites) {
    const countryName = s.country || 'Other';
    let country = countries.get(countryName);
    if (!country) countries.set(countryName, (country = { name: countryName, kind: 'Country', children: [] }));
    const buildings = lk.buildings.filter((b) => b.siteId === s.id);
    const node: SiteNode = {
      name: s.name, kind: 'Site', code: s.code, type: s.type, address: s.address, building: buildings[0]?.name ?? '',
      floors: lk.floors.filter((f) => f.siteId === s.id).length,
      children: buildings.length ? buildings.map((b) => ({ name: b.name, kind: 'Building' as const })) : undefined,
    };
    let parent = country;
    if (s.city && s.city !== countryName) {
      parent = country.children!.find((c) => c.kind === 'City' && c.name === s.city) ?? parent;
      if (parent === country) country.children!.push((parent = { name: s.city, kind: 'City', children: [] }));
    }
    parent.children!.push(node);
  }
  return [...countries.values()];
}

/** Where an asset sits now, for the pickers. */
export interface PoolItem extends PoolAsset {
  assetRegistrationId: string;
  category: string;
  location: string;
  manufacturer: string;
  assetStatus: string;
}

const PAGE = 200;
/** Pages read at most when loading every assignment / asset (1,000 records). */
const MAX_PAGES = 5;

/** Asset Assignment API: /api/asset-assignments, plus read-only assets, master data and event logs. */
@Injectable({ providedIn: 'root' })
export class AssetAssignmentService {
  private readonly http = inject(HttpClient);
  private readonly auth = inject(AuthService);

  private readonly url = `${ASSET_API}asset-assignments`;
  private readonly mdm = `${environment.masterDataApiUrl}api/master-data/`;
  private lookups$?: Observable<AssignmentLookups>;

  /** Signed-in user, for createdBy / updatedBy. */
  get user(): string {
    return this.auth.getUserEmail() || '';
  }

  // ===== Assignments =====

  /** Every assignment (paged through, newest first). `status` narrows, e.g. "Draft". */
  listAll(status?: string): Observable<AssetAssignmentDto[]> {
    const page = (n: number) => {
      let params = new HttpParams().set('page', n).set('pageSize', PAGE);
      if (status) params = params.set('status', status);
      return this.http.get<AssetAssignmentListDto>(this.url, { params }).pipe(map((r) => ({ n, r })));
    };
    return page(1).pipe(
      expand(({ n, r }) => (n * PAGE < r.totalCount && n < MAX_PAGES ? page(n + 1) : of())),
      reduce((all, { r }) => all.concat(r.items ?? []), [] as AssetAssignmentDto[]),
    );
  }

  get(id: string): Observable<AssetAssignmentDto> {
    return this.http.get<AssetAssignmentDto>(`${this.url}/${encodeURIComponent(id)}`);
  }

  /** Confirm Assignment, or Save as Draft. */
  create(request: AssetAssignmentRequest, saveAsDraft = false): Observable<AssetAssignmentDto> {
    const body: AssetAssignmentRequest = { ...request, createdBy: request.createdBy || this.user || null, clientId: TENANT.clientId, tenantId: TENANT.tenantId };
    return this.http.post<AssetAssignmentDto>(this.url, body, { params: saveAsDraft ? { saveAsDraft: true } : {} });
  }

  update(id: string, request: AssetAssignmentRequest, saveAsDraft = false): Observable<AssetAssignmentDto> {
    return this.http.put<AssetAssignmentDto>(`${this.url}/${encodeURIComponent(id)}`, request, { params: saveAsDraft ? { saveAsDraft: true } : {} });
  }

  delete(id: string): Observable<void> {
    return this.http.delete<void>(`${this.url}/${encodeURIComponent(id)}`);
  }

  /** End Assignment for some (or, with none given, all) of its assets; returns the returned assignment. */
  returnAssets(id: string, assetRegistrationIds: string[] = [], remarks?: string): Observable<AssetAssignmentDto> {
    return this.http.post<AssetAssignmentDto>(`${this.url}/${encodeURIComponent(id)}/return`, {
      assetRegistrationIds, returnDate: isoDate(new Date()), remarks: remarks || null, updatedBy: this.user || null,
    });
  }

  /** Ends the current assignments of these rows' assets, one call per assignment, one after another. */
  returnRows(rows: { assignmentId: string; assetRegistrationId: string }[], remarks?: string): Observable<AssetAssignmentDto[]> {
    const groups = new Map<string, string[]>();
    for (const r of rows) groups.set(r.assignmentId, [...(groups.get(r.assignmentId) ?? []), r.assetRegistrationId]);
    const calls = [...groups].map(([id, assets]) => this.returnAssets(id, assets, remarks));
    return calls.length ? concat(...calls).pipe(toArray()) : of([]);
  }

  /** Changes made to an assignment: GET /api/eventlogs for it, oldest first. */
  history(id: string): Observable<AssignmentEvent[]> {
    const params = new HttpParams().set('entityType', 'AssetAssignment').set('entityId', id).set('pageSize', 100);
    return this.http.get<{ items: EventLogDto[] }>(EVENTLOG_API, { params }).pipe(
      map((r) =>
        (r.items ?? [])
          .map((e) => ({ at: e.createdAt ? stamp(new Date(e.createdAt)) : '', action: e.action, by: e.performedBy ?? '', note: e.entityName || e.message || '' }))
          .sort((a, b) => a.at.localeCompare(b.at)),
      ),
    );
  }

  /** Uploads an attachment through the Media service; returns its link. */
  uploadFile(file: File): Observable<AssignmentAttachmentDto> {
    const form = new FormData();
    form.append('file', file, file.name);
    form.append('category', 'asset-assignments');
    return this.http
      .post<{ url: string }>(`${MEDIA_API}upload`, form)
      .pipe(map((r) => ({ fileName: file.name, fileUrl: r.url, contentType: file.type || null, fileSize: file.size })));
  }

  // ===== Assets (read only) =====

  /** Registered assets for the pickers (up to 1,000). */
  listAssets(): Observable<PoolItem[]> {
    const page = (n: number) =>
      this.http
        .get<AssetListDto>(`${ASSET_API}asset-registrations`, { params: new HttpParams().set('page', n).set('pageSize', PAGE).set('sortBy', 'assetName') })
        .pipe(map((r) => ({ n, r })));
    return page(1).pipe(
      expand(({ n, r }) => (n * PAGE < r.totalCount && n < MAX_PAGES ? page(n + 1) : of())),
      takeWhile(({ r }) => (r.items ?? []).length > 0, true),
      reduce((all, { r }) => all.concat((r.items ?? []).map(toPoolItem)), [] as PoolItem[]),
    );
  }

  /** Assets matching a tag, serial number or name (server search), e.g. for a scanned serial number. */
  searchAssets(search: string): Observable<PoolItem[]> {
    const params = new HttpParams().set('page', 1).set('pageSize', 20).set('search', search);
    return this.http.get<AssetListDto>(`${ASSET_API}asset-registrations`, { params }).pipe(map((r) => (r.items ?? []).map(toPoolItem)));
  }

  // ===== Master data (read only) =====

  /** Employees, departments, sites, buildings, floors and rooms; loaded once. A list that fails to load is empty. */
  lookups(): Observable<AssignmentLookups> {
    if (!this.lookups$) {
      const list = <T>(resource: string) =>
        this.http.get<T[]>(this.mdm + resource, { params: { clientId: environment.clientId } }).pipe(catchError(() => of([] as T[])));
      this.lookups$ = forkJoin({
        employees: list<MdmEmployeeDto>('employees'),
        departments: list<MdmDepartmentDto>('departments'),
        sites: list<MdmSiteDto>('sites'),
        buildings: list<MdmBuildingDto>('buildings'),
        floors: list<MdmFloorDto>('floors'),
        rooms: list<MdmLocationDto>('locations'),
      }).pipe(
        map(toLookups),
        shareReplay(1),
      );
    }
    return this.lookups$;
  }

  /** Drops the cached master data so the next lookups() reloads it. */
  refreshLookups(): void {
    this.lookups$ = undefined;
  }

  /** Every assignment plus the lookups, for pages that need both. */
  loadAll(): Observable<{ assignments: AssetAssignmentDto[]; lookups: AssignmentLookups }> {
    return forkJoin({ assignments: this.listAll(), lookups: this.lookups() });
  }
}

function toPoolItem(a: AssetListItemDto): PoolItem {
  return {
    assetRegistrationId: a.assetRegistrationId,
    assetName: a.assetName || a.assetRegistrationId,
    assetType: a.assetType ?? '',
    kind: kindOf(`${a.assetType ?? ''} ${a.category ?? ''} ${a.assetName ?? ''}`),
    tag: a.assetTag || a.assetRegistrationId,
    serial: '',
    category: a.category ?? '',
    location: a.locationPath?.replace(/ > /g, ' - ') || [a.siteName, a.buildingName, a.floorName, a.roomName].filter(Boolean).join(' - '),
    manufacturer: a.manufacturer ?? '',
    assetStatus: a.status ?? '',
  };
}

function toLookups(r: {
  employees: MdmEmployeeDto[];
  departments: MdmDepartmentDto[];
  sites: MdmSiteDto[];
  buildings: MdmBuildingDto[];
  floors: MdmFloorDto[];
  rooms: MdmLocationDto[];
}): AssignmentLookups {
  const employees: LookupEmployee[] = r.employees.map((e) => ({
    mdmId: e.id,
    name: e.employeeName,
    id: e.employeeId,
    title: e.designation ?? '',
    department: e.department?.name ?? '',
    departmentId: e.department?.id ?? '',
    location: e.site ?? '',
    costCenter: e.costCenter ?? '',
    managerId: e.reportingManager?.id ?? '',
    managerName: e.reportingManager?.name ?? '',
    active: isActiveStatus(e.status),
  }));
  const departments: LookupDepartment[] = r.departments.map((d) => {
    const head = d.departmentHead?.id ? employees.find((e) => e.mdmId === d.departmentHead!.id) : undefined;
    const manager = d.departmentHead?.name || head
      ? { mdmId: d.departmentHead?.id ?? '', name: head?.name || d.departmentHead?.name || '', id: head?.id ?? '', title: head?.title ?? '', location: head?.location ?? '' }
      : undefined;
    return { id: d.id, name: d.departmentName, code: d.code, manager, active: isActiveStatus(d.status) };
  });
  const sites: LookupSite[] = r.sites.map((s) => ({
    id: s.id,
    name: s.siteName,
    code: s.code,
    type: s.siteType ?? '',
    city: s.city || s.address?.city || '',
    country: s.country || s.address?.country || '',
    address: [s.address?.line1, s.address?.line2, s.address?.city || s.city, s.address?.country || s.country].filter(Boolean).join(', '),
  }));
  const buildings: LookupPlace[] = r.buildings.map((b) => ({ id: b.id, name: b.name, code: b.code, siteId: b.site?.id ?? '', buildingId: b.id, buildingName: b.name, floorName: '' }));
  const floors: LookupPlace[] = r.floors.map((f) => ({ id: f.id, name: f.name, code: f.code, siteId: f.site?.id ?? '', buildingId: f.buildingId, buildingName: f.buildingName, floorName: f.name }));
  const rooms: LookupPlace[] = r.rooms.map((l) => ({
    id: l.id, name: l.locationName, code: l.code, siteId: l.site?.id ?? '', buildingId: buildings.find((b) => b.name === l.building)?.id ?? '', buildingName: l.building ?? '', floorName: l.floor ?? '',
  }));
  return { employees, departments, sites, buildings, floors, rooms };
}

/** Unique sorted non-empty values. */
export function uniqueSorted(values: (string | null | undefined)[]): string[] {
  return [...new Set(values.filter((v): v is string => !!v))].sort((a, b) => a.localeCompare(b));
}
