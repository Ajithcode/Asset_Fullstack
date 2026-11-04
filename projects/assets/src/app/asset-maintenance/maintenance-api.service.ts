import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, catchError, forkJoin, map, of, switchMap } from 'rxjs';
import { AssetRecord, AuthService, TODAY, environment, toIsoDate } from 'shared-ui';
import { ASSET_API, TENANT, apiErrorMessage } from '../services/asset-api';
import { AssetListItemDto, AssetLookupService, isoDay } from './asset-lookup.service';
import type { MaintenanceData, WorkOrder } from './asset-maintenance';
import { PartUsage, SparePart } from './maintenance-parts';
import { PmSchedule, addMonths, categoryOf, frequencyMonths } from './maintenance-pm';
import { SR_CATEGORIES, ServiceRequest, SrCategory, SrPriority, SrStatus } from './maintenance-sr';

/** Maintenance service routes, reached through the API gateway. */
export const MAINTENANCE_API = `${environment.maintenanceApiUrl}api/`;

// ===== DTOs (camelCase JSON of A360.Maintenance.Api/Contracts *Response records) =====
// Fields marked optional are only returned by newer versions of the Maintenance service.

interface AuditDto {
  id: string;
  createdBy?: string | null;
  createdAt?: string | null;
  updatedBy?: string | null;
  updatedAt?: string | null;
  clientId?: string | null;
  tenantId?: string | null;
  status?: string | null;
  isDeleted?: boolean;
}

export interface WorkOrderDto extends AuditDto {
  workOrderId: string;
  assetId: string;
  assetName: string;
  workType: string;
  title: string;
  description: string | null;
  priority: string;
  scheduledDate: string | null;
  assignedTechnician: string | null;
  dueDate?: string | null;
  plannedStartDate?: string | null;
  actualEndDate?: string | null;
  parts?: { partNumber?: string | null; partName: string; quantity: number; unitCost: number }[];
  partsCost?: number;
  labourCost?: number;
  additionalCost?: number;
}

export interface PreventiveMaintenanceDto extends AuditDto {
  pmScheduleId: string;
  assetId: string;
  assetName: string;
  frequency: string;
  triggerType: string;
  lastMaintenanceDate: string | null;
  nextDueDate: string | null;
  autoCreateWorkOrder: boolean;
  scheduleName?: string | null;
  description?: string | null;
  assignedTo?: string | null;
  estimatedHours?: number | null;
  checklist?: string[];
}

interface SparePartDto extends AuditDto {
  partId: string;
  assetId: string | null;
  assetName: string | null;
  partName: string;
  category: string | null;
  quantityInStock: number;
  minimumStockLevel: number;
  unitCost: number;
  supplier: string | null;
}

interface PredictiveDto extends AuditDto {
  assetId: string;
  sensorType: string;
  thresholdValue: number;
  alertCondition: string | null;
  riskLevel: string;
  lastReading?: number | null;
}

interface DowntimeDto extends AuditDto {
  assetId: string;
  downtimeStart: string | null;
  downtimeEnd: string | null;
  totalDowntime: string | null;
  reasonForDowntime: string | null;
}

interface VendorAmcDto extends AuditDto {
  vendorName: string;
  contractId: string;
  endDate: string | null;
}

// ===== Asset API service requests (AssetServiceRequestContracts.cs) =====

interface SrListItemDto {
  id: string;
  requestNumber: string;
  title: string;
  status: string | null;
  assetRegistrationId: string;
  assetTag: string | null;
  requestType: string | null;
  priority: string | null;
  requestedDate: string | null;
  targetDate: string | null;
  location: string | null;
  requestedBy: string | null;
  assignedTo: string | null;
  workOrderId: string | null;
}

interface SrPersonDto {
  userId?: string | null;
  name: string;
  role?: string | null;
}

interface SrAttachmentDto {
  attachmentId: string;
  fileName: string;
  fileUrl: string;
  fileSize: number | null;
  description: string | null;
  uploadedBy: string | null;
  uploadedAt: string | null;
}

export interface SrDto {
  id: string;
  requestNumber: string;
  status: string | null;
  asset: { assetRegistrationId: string; assetTag: string | null; assetName: string | null };
  title: string;
  requestType: string | null;
  priority: string | null;
  requestedDate: string | null;
  targetDate: string | null;
  description: string | null;
  location: { id: string | null; name: string } | null;
  department: { id: string | null; name: string } | null;
  requestedBy: SrPersonDto | null;
  assignedTo: SrPersonDto | null;
  estimatedDowntimeHours: number | null;
  plannedStart: string | null;
  plannedEnd: string | null;
  actualStart: string | null;
  actualEnd: string | null;
  workOrder: { workOrderId: string } | null;
  relatedServiceRequest: string | null;
  tags: string[];
  referenceNumber: string | null;
  attachments: SrAttachmentDto[];
  createdAt: string | null;
}

interface SrUpdateDto {
  updateType: string;
  action: string;
  message: string | null;
  status: string | null;
  userName: string | null;
  createdAt: string;
}

/** What the page shows while the asset's records load (and when they cannot be loaded). */
export function emptyMaintenanceData(asset: AssetRecord): MaintenanceData {
  return {
    year: TODAY.getFullYear(),
    lastYear: { cost: 0, downtimeMin: 0 },
    lastMonthOpen: 0,
    workOrders: [],
    serviceRequests: [],
    spareParts: [],
    partsUsage: [],
    partsReserved: [],
    sensors: [],
    overview: { serialNumber: asset.serialNumber ?? '', warrantyYears: 0, amcVendor: '' },
    activity: [],
    pm: [],
    pmLastYear: 0,
    pmTemplates: [],
  };
}

// ===== Mapping =====

const WO_TYPES: WorkOrder['type'][] = ['Preventive', 'Corrective', 'Inspection', 'Service'];

function woType(value: string | null | undefined): WorkOrder['type'] {
  const v = (value ?? '').toLowerCase();
  return WO_TYPES.find((t) => t.toLowerCase() === v) ?? (v.includes('predict') || v.includes('inspect') ? 'Inspection' : v.includes('service') ? 'Service' : v.includes('prevent') ? 'Preventive' : 'Corrective');
}

function woStatus(value: string | null | undefined): WorkOrder['status'] | null {
  switch ((value ?? '').toLowerCase()) {
    case 'completed':
    case 'closed':
      return 'Completed';
    case 'in progress':
    case 'on hold':
      return 'In Progress';
    case 'scheduled':
      return 'Scheduled';
    case 'cancelled':
      return null;
    default:
      return 'Open';
  }
}

function priority(value: string | null | undefined): SrPriority {
  const v = (value ?? '').toLowerCase();
  return v === 'high' || v === 'critical' ? 'High' : v === 'low' ? 'Low' : 'Medium';
}

/** Minutes between two API date-times (0 when either is missing). */
function minutesBetween(from: string | null, to: string | null): number {
  if (!from || !to) return 0;
  return Math.max(0, Math.round((new Date(to).getTime() - new Date(from).getTime()) / 60_000));
}

/** "2h 30m", "150", "2.5 hours" → minutes. */
function parseDuration(text: string | null): number {
  if (!text) return 0;
  const h = /(\d+(?:\.\d+)?)\s*h/i.exec(text);
  const m = /(\d+)\s*m/i.exec(text);
  if (h || m) return Math.round(Number(h?.[1] ?? 0) * 60 + Number(m?.[1] ?? 0));
  const n = Number(text);
  return Number.isFinite(n) ? Math.round(n) : 0;
}

export function toWorkOrder(w: WorkOrderDto): WorkOrder | null {
  const status = woStatus(w.status);
  if (!status || w.isDeleted) return null;
  return {
    id: w.workOrderId || w.id,
    apiId: w.id,
    date: isoDay(w.scheduledDate ?? w.dueDate ?? w.plannedStartDate ?? w.createdAt) || toIsoDate(TODAY),
    type: woType(w.workType),
    description: w.title || w.description || '',
    labor: w.labourCost ?? 0,
    parts: w.partsCost ?? 0,
    service: w.additionalCost ?? 0,
    downtimeMin: 0,
    reason: '',
    status,
    technician: w.assignedTechnician ?? '',
    priority: priority(w.priority),
  };
}

export function toPmSchedule(p: PreventiveMaintenanceDto): PmSchedule {
  const name = p.scheduleName?.trim() || `${p.frequency} ${p.triggerType ? p.triggerType.toLowerCase() + ' ' : ''}maintenance`.trim();
  const lastPerformed = isoDay(p.lastMaintenanceDate);
  const nextDue = isoDay(p.nextDueDate) || (lastPerformed ? addMonths(lastPerformed, frequencyMonths(p.frequency)) : toIsoDate(TODAY));
  const checklist = p.checklist ?? [];
  return {
    id: p.pmScheduleId || p.id,
    apiId: p.id,
    name,
    description: p.description ?? '',
    component: 'Main unit',
    category: categoryOf(name),
    frequency: p.frequency,
    lastPerformed,
    nextDue,
    assignedTo: p.assignedTo ?? 'Maintenance Team',
    durationHours: p.estimatedHours ?? 0,
    estCost: 0,
    template: '',
    parts: 'as per checklist',
    match: [name.split(/[ ,]/)[0].toLowerCase()],
    tasks: checklist,
    checklist,
    documents: [],
  };
}

function toSparePart(p: SparePartDto, asset: AssetRecord): SparePart {
  return {
    partNo: p.partId || p.id,
    name: p.partName,
    category: p.category ?? 'General',
    unitCost: p.unitCost ?? 0,
    onHand: p.quantityInStock ?? 0,
    reorderLevel: p.minimumStockLevel ?? 0,
    onOrder: 0,
    leadTimeDays: 0,
    manufacturer: p.supplier ?? '',
    compatible: asset.model,
    critical: false,
    bomQty: 0,
    replaceEveryDays: 0,
  };
}

const SENSOR_UNITS: Record<string, string> = { temperature: '°C', vibration: 'mm/s', pressure: 'bar', current: 'A', voltage: 'V', humidity: '%', 'oil quality': '%', 'runtime hours': 'h' };

function toSensor(p: PredictiveDto): MaintenanceData['sensors'][number] | null {
  if (p.lastReading === null || p.lastReading === undefined) return null;
  const low = /below/i.test(p.alertCondition ?? '');
  const t = p.thresholdValue ?? 0;
  return { name: p.sensorType, value: p.lastReading, unit: SENSOR_UNITS[p.sensorType.toLowerCase()] ?? '', warn: low ? t * 1.1 : t * 0.9, alarm: t, trend: 'stable', ...(low ? { low } : {}) };
}

/** API status → the four statuses the Service Requests tab shows. */
function srStatus(value: string | null | undefined): SrStatus {
  switch ((value ?? '').toLowerCase()) {
    case 'in progress':
    case 'on hold':
    case 'approved':
      return 'In Progress';
    case 'closed':
      return 'Resolved';
    case 'cancelled':
      return 'Closed';
    default:
      return 'Open';
  }
}

/** Tab status → the status sent with an update (Resolved and Closed both close the request; Cancelled is left to the request page). */
export function apiSrStatus(status: SrStatus): string {
  return status === 'Resolved' || status === 'Closed' ? 'Closed' : status;
}

function srCategory(tags: string[], requestType: string | null): SrCategory {
  const tagged = SR_CATEGORIES.find((c) => tags.some((t) => t.toLowerCase() === c.toLowerCase()));
  if (tagged) return tagged;
  const t = (requestType ?? '').toLowerCase();
  return t === 'other' ? 'Others' : t === 'calibration' || t === 'installation' ? 'Electrical' : t === 'breakdown' || t === 'corrective' ? 'Mechanical' : 'General';
}

/** yyyy-MM-ddTHH:mm in local time ('' when missing). */
function localStamp(value: string | null | undefined): string {
  if (!value) return '';
  const d = new Date(value);
  if (Number.isNaN(d.getTime())) return '';
  const p = (n: number) => String(n).padStart(2, '0');
  return `${toIsoDate(d)}T${p(d.getHours())}:${p(d.getMinutes())}`;
}

function fileSize(bytes: number | null): string {
  if (!bytes) return '';
  return bytes >= 1_048_576 ? `${(bytes / 1_048_576).toFixed(1)} MB` : `${Math.max(1, Math.round(bytes / 1024))} KB`;
}

export function toServiceRequest(item: SrListItemDto, detail: SrDto | null, updates: SrUpdateDto[]): ServiceRequest {
  const createdAt = localStamp(detail?.requestedDate ?? item.requestedDate ?? detail?.createdAt);
  const ordered = [...updates].sort((a, b) => a.createdAt.localeCompare(b.createdAt));
  const firstResponse = ordered.find((u) => u.updateType !== 'Created');
  const closed = [...ordered].reverse().find((u) => (u.status ?? '').toLowerCase() === 'closed');
  const status = srStatus(item.status);
  const done = status === 'Resolved' || status === 'Closed';
  return {
    id: item.requestNumber || item.id,
    apiId: item.id,
    date: createdAt.slice(0, 10),
    title: item.title,
    description: detail?.description ?? '',
    category: srCategory(detail?.tags ?? [], item.requestType),
    priority: priority(item.priority),
    status,
    createdAt,
    requestedBy: item.requestedBy ?? '',
    requesterDept: detail?.department?.name ?? '',
    assignedTo: item.assignedTo ?? '',
    respondedAt: localStamp(detail?.actualStart ?? firstResponse?.createdAt),
    resolvedAt: done ? localStamp(detail?.actualEnd ?? closed?.createdAt ?? detail?.createdAt) : '',
    targetDate: isoDay(item.targetDate),
    workOrder: item.workOrderId ?? '',
    rating: 0,
    updates: ordered.map((u) => ({ at: localStamp(u.createdAt), by: u.userName ?? '', text: u.message || u.action })),
    attachments: (detail?.attachments ?? []).map((a) => ({
      name: a.fileName, size: fileSize(a.fileSize), by: a.uploadedBy ?? undefined, at: localStamp(a.uploadedAt) || undefined, description: a.description ?? undefined, url: a.fileUrl,
    })),
    location: detail?.location?.name ?? item.location ?? undefined,
    downtimeHours: detail?.estimatedDowntimeHours ?? undefined,
    plannedStart: localStamp(detail?.plannedStart) || undefined,
    plannedEnd: localStamp(detail?.plannedEnd) || undefined,
    relatedRequest: detail?.relatedServiceRequest ?? undefined,
    tags: detail?.tags,
    reference: detail?.referenceNumber ?? undefined,
  };
}

/** Everything the Maintenance page reads for one asset. `errors` lists the sources that failed. */
export interface MaintenanceLoad {
  registration: AssetListItemDto | null;
  workOrders: WorkOrder[];
  partsUsage: PartUsage[];
  serviceRequests: ServiceRequest[];
  spareParts: SparePart[];
  pm: PmSchedule[];
  sensors: MaintenanceData['sensors'];
  amcVendor: string;
  errors: string[];
}

/** Builds the page's data from what the APIs returned; the year figures are worked out from the work orders. */
export function buildMaintenanceData(asset: AssetRecord, load: MaintenanceLoad): MaintenanceData {
  const d = emptyMaintenanceData(asset);
  const year = d.year;
  const monthStart = toIsoDate(new Date(TODAY.getFullYear(), TODAY.getMonth(), 1));
  const lastYear = load.workOrders.filter((w) => w.status === 'Completed' && w.date.startsWith(String(year - 1)));
  d.workOrders = load.workOrders;
  d.partsUsage = load.partsUsage;
  d.serviceRequests = load.serviceRequests;
  d.spareParts = load.spareParts;
  d.pm = load.pm;
  d.sensors = load.sensors;
  d.overview.amcVendor = load.amcVendor;
  d.lastYear = { cost: lastYear.reduce((t, w) => t + w.labor + w.parts + w.service, 0), downtimeMin: lastYear.reduce((t, w) => t + w.downtimeMin, 0) };
  // open at the start of this month: raised before it and not completed since
  d.lastMonthOpen = load.workOrders.filter((w) => w.date < monthStart && w.status !== 'Completed').length;
  d.pmLastYear = load.workOrders.filter((w) => w.type === 'Preventive' && w.status === 'Completed' && w.date.startsWith(String(year - 1))).length;
  return d;
}

/** Maintenance records of one asset: work orders, PM, parts and sensors (Maintenance service) and service requests (Asset API). */
@Injectable({ providedIn: 'root' })
export class MaintenanceApiService {
  private readonly http = inject(HttpClient);
  private readonly auth = inject(AuthService);
  private readonly lookup = inject(AssetLookupService);
  /** Last read API records, so updates can send the whole record back. */
  private readonly workOrderDtos = new Map<string, WorkOrderDto>();
  private readonly pmDtos = new Map<string, PreventiveMaintenanceDto>();

  get user(): string {
    return this.auth.getUser()?.userName || this.auth.getUserEmail() || 'unknown';
  }

  /** Loads everything for the asset; sources that fail are reported in `errors` and left empty. */
  load(asset: AssetRecord): Observable<MaintenanceLoad> {
    const errors: string[] = [];
    const tag = encodeURIComponent(asset.tag);
    const get = <T>(label: string, url: string, fallback: T): Observable<T> =>
      this.http.get<T>(url).pipe(
        catchError((err) => {
          errors.push(`${label}: ${apiErrorMessage(err)}`);
          return of(fallback);
        }),
      );
    return forkJoin({
      registration: this.lookup.byTag(asset.tag).pipe(
        catchError((err) => {
          errors.push(`Asset: ${apiErrorMessage(err)}`);
          return of(null);
        }),
      ),
      workOrders: get<WorkOrderDto[]>('Work orders', `${MAINTENANCE_API}work-orders/asset/${tag}`, []),
      pm: get<PreventiveMaintenanceDto[]>('PM schedules', `${MAINTENANCE_API}preventive-maintenance/asset/${tag}`, []),
      parts: get<SparePartDto[]>('Spare parts', `${MAINTENANCE_API}spare-parts/asset/${tag}`, []),
      predictive: get<PredictiveDto[]>('Condition monitoring', `${MAINTENANCE_API}predictive-maintenance/asset/${tag}`, []),
      downtime: get<DowntimeDto[]>('Downtime', `${MAINTENANCE_API}downtime-tracking/asset/${tag}`, []),
      amc: get<VendorAmcDto[]>('AMC', `${MAINTENANCE_API}vendor-amc/asset/${tag}`, []),
    }).pipe(
      switchMap((r) =>
        this.serviceRequests(r.registration, errors).pipe(
          map((serviceRequests) => {
            this.workOrderDtos.clear();
            this.pmDtos.clear();
            const live = r.workOrders.filter((w) => !w.isDeleted);
            live.forEach((w) => this.workOrderDtos.set(w.id, w));
            r.pm.filter((p) => !p.isDeleted).forEach((p) => this.pmDtos.set(p.id, p));
            const workOrders = live.map(toWorkOrder).filter((w): w is WorkOrder => !!w);
            // downtime records add to the work order done on the same day
            for (const t of r.downtime.filter((x) => !x.isDeleted)) {
              const day = isoDay(t.downtimeStart);
              const wo = workOrders.find((w) => w.date === day);
              if (!wo) continue;
              wo.downtimeMin += minutesBetween(t.downtimeStart, t.downtimeEnd) || parseDuration(t.totalDowntime);
              wo.reason ||= t.reasonForDowntime ?? 'Others';
            }
            const partsUsage: PartUsage[] = live.flatMap((w) =>
              (w.parts ?? []).map((p) => ({ wo: w.workOrderId || w.id, date: isoDay(w.actualEndDate ?? w.scheduledDate ?? w.createdAt), partNo: p.partNumber || p.partName, qty: p.quantity, unitCost: p.unitCost })),
            );
            const today = toIsoDate(TODAY);
            const amc = r.amc.filter((a) => !a.isDeleted).sort((a, b) => (b.endDate ?? '').localeCompare(a.endDate ?? ''));
            return {
              registration: r.registration,
              workOrders,
              partsUsage,
              serviceRequests,
              spareParts: r.parts.filter((p) => !p.isDeleted).map((p) => toSparePart(p, asset)),
              pm: r.pm.filter((p) => !p.isDeleted && (p.status ?? 'Active') !== 'Inactive').map(toPmSchedule),
              sensors: r.predictive.filter((p) => !p.isDeleted).map(toSensor).filter((s): s is NonNullable<typeof s> => !!s),
              amcVendor: (amc.find((a) => isoDay(a.endDate) >= today) ?? amc[0])?.vendorName ?? '',
              errors,
            };
          }),
        ),
      ),
    );
  }

  /** The asset's service requests with their details and updates (newest first). */
  private serviceRequests(reg: AssetListItemDto | null, errors: string[]): Observable<ServiceRequest[]> {
    if (!reg) return of([]);
    return this.http
      .get<{ items: SrListItemDto[] }>(`${ASSET_API}service-requests`, { params: { assetRegistrationId: reg.assetRegistrationId, page: 1, pageSize: 200 } })
      .pipe(
        switchMap((res) => {
          const items = res.items.filter((i) => (i.status ?? '') !== 'Draft');
          if (!items.length) return of([]);
          return forkJoin(
            items.map((i) =>
              forkJoin({
                detail: this.http.get<SrDto>(`${ASSET_API}service-requests/${i.id}`).pipe(catchError(() => of(null))),
                updates: this.http.get<{ items: SrUpdateDto[] }>(`${ASSET_API}service-requests/${i.id}/updates`).pipe(
                  map((u) => u.items),
                  catchError(() => of([] as SrUpdateDto[])),
                ),
              }).pipe(map(({ detail, updates }) => toServiceRequest(i, detail, updates))),
            ),
          );
        }),
        catchError((err) => {
          errors.push(`Service requests: ${apiErrorMessage(err)}`);
          return of([]);
        }),
      );
  }

  // ===== Work orders =====

  createWorkOrder(asset: AssetRecord, f: { id: string; type: string; description: string; date: string; technician: string; priority?: string }): Observable<WorkOrder> {
    const body = {
      workOrderId: f.id, assetId: asset.tag, assetName: asset.name, workType: f.type, title: f.description, description: f.description,
      priority: f.priority ?? 'Medium', scheduledDate: f.date, assignedTechnician: f.technician, status: 'Open', createdBy: this.user, ...TENANT,
    };
    return this.http.post<WorkOrderDto>(`${MAINTENANCE_API}work-orders`, body).pipe(
      map((dto) => {
        this.workOrderDtos.set(dto.id, dto);
        return toWorkOrder(dto) ?? ({ ...body, id: f.id, apiId: dto.id } as unknown as WorkOrder);
      }),
    );
  }

  setWorkOrderStatus(w: WorkOrder, status: string): Observable<unknown> {
    const dto = w.apiId ? this.workOrderDtos.get(w.apiId) : undefined;
    if (!w.apiId || !dto) return of(null);
    const body = {
      assetId: dto.assetId, assetName: dto.assetName, workType: dto.workType, title: dto.title, description: dto.description, priority: dto.priority,
      scheduledDate: dto.scheduledDate, assignedTechnician: dto.assignedTechnician, updatedBy: this.user, status,
    };
    return this.http.put<WorkOrderDto>(`${MAINTENANCE_API}work-orders/${w.apiId}`, body).pipe(map((saved) => this.workOrderDtos.set(saved.id, saved)));
  }

  // ===== Preventive maintenance =====

  savePm(asset: AssetRecord, s: PmSchedule): Observable<PmSchedule> {
    const dto = s.apiId ? this.pmDtos.get(s.apiId) : undefined;
    const common = {
      assetId: asset.tag, assetName: asset.name, frequency: s.frequency, triggerType: dto?.triggerType || 'Time Based',
      lastMaintenanceDate: s.lastPerformed || null, nextDueDate: s.nextDue || null, autoCreateWorkOrder: dto?.autoCreateWorkOrder ?? false,
      // newer Maintenance services keep these too
      scheduleName: s.name, description: s.description, assignedTo: s.assignedTo, estimatedHours: s.durationHours, checklist: s.checklist,
    };
    const req = dto
      ? this.http.put<PreventiveMaintenanceDto>(`${MAINTENANCE_API}preventive-maintenance/${dto.id}`, { ...common, updatedBy: this.user, status: dto.status ?? 'Active' })
      : this.http.post<PreventiveMaintenanceDto>(`${MAINTENANCE_API}preventive-maintenance`, { ...common, pmScheduleId: s.id, createdBy: this.user, status: 'Active', ...TENANT });
    return req.pipe(
      map((saved) => {
        this.pmDtos.set(saved.id, saved);
        return { ...s, apiId: saved.id };
      }),
    );
  }

  deletePm(s: PmSchedule): Observable<unknown> {
    return s.apiId ? this.http.delete(`${MAINTENANCE_API}preventive-maintenance/${s.apiId}`) : of(null);
  }

  // ===== Service requests (Asset API) =====

  /** Creates (Submit) or edits a request; returns the saved request as the tab shows it. */
  saveServiceRequest(
    registrationId: string,
    r: { apiId?: string; title: string; description: string; category: SrCategory; priority: SrPriority; requestedBy: string; requesterDept: string; assignedTo: string; targetDate: string },
  ): Observable<ServiceRequest> {
    const body = {
      assetRegistrationId: registrationId,
      title: r.title,
      requestType: r.category === 'Others' ? 'Other' : 'Corrective',
      priority: r.priority,
      targetDate: r.targetDate || null,
      description: r.description || r.title,
      department: r.requesterDept ? { id: null, name: r.requesterDept } : null,
      requestedBy: { userId: null, name: r.requestedBy || this.user, role: null },
      assignedTo: r.assignedTo ? { userId: null, name: r.assignedTo, role: null } : null,
      tags: [r.category],
      createdBy: this.user,
      ...TENANT,
    };
    const req = r.apiId
      ? this.http.put<SrDto>(`${ASSET_API}service-requests/${r.apiId}`, body)
      : this.http.post<SrDto>(`${ASSET_API}service-requests`, body);
    return req.pipe(map((dto) => this.fromDetail(dto)));
  }

  /** Comment and/or status change (shown on the request's Updates timeline). */
  addServiceRequestUpdate(apiId: string, message: string, status?: SrStatus): Observable<unknown> {
    return this.http.post(`${ASSET_API}service-requests/${apiId}/updates`, { message, status: status ? apiSrStatus(status) : null });
  }

  linkWorkOrder(apiId: string, workOrderId: string): Observable<unknown> {
    return this.http.post(`${ASSET_API}service-requests/${apiId}/work-orders`, { workOrderId });
  }

  deleteServiceRequest(apiId: string): Observable<unknown> {
    return this.http.delete(`${ASSET_API}service-requests/${apiId}`);
  }

  private fromDetail(dto: SrDto): ServiceRequest {
    const item: SrListItemDto = {
      id: dto.id, requestNumber: dto.requestNumber, title: dto.title, status: dto.status, assetRegistrationId: dto.asset.assetRegistrationId, assetTag: dto.asset.assetTag,
      requestType: dto.requestType, priority: dto.priority, requestedDate: dto.requestedDate, targetDate: dto.targetDate, location: dto.location?.name ?? null,
      requestedBy: dto.requestedBy?.name ?? null, assignedTo: dto.assignedTo?.name ?? null, workOrderId: dto.workOrder?.workOrderId ?? null,
    };
    return toServiceRequest(item, dto, [{ updateType: 'Created', action: 'Service request created.', message: null, status: dto.status, userName: dto.requestedBy?.name ?? null, createdAt: dto.createdAt ?? new Date().toISOString() }]);
  }
}
