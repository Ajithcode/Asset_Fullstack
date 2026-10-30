import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, catchError } from 'rxjs';
import { AuthService, environment } from 'shared-ui';
import { apiError } from '../geofence-alerts/geofence-api.service';
import { Device, DeviceKind, MaintType, PLANS, Task, TaskStatus } from './maintenance-data';

type ApiStatus = TaskStatus | 'Cancelled';

/** A maintenance job as the API stores it ({trackingApiUrl}api/device-maintenance, addressed by maintenanceId, e.g. DM000001). */
export interface MaintenanceDto {
  id: string;
  maintenanceId: string;
  deviceId: string;
  deviceName: string | null;
  model: string;
  assetRegistrationId: string | null;
  assetName: string | null;
  siteId: string | null;
  siteName: string;
  maintenanceType: MaintType;
  dueDate: string;
  assignedTo: string | null;
  status: ApiStatus | null;
  updatedAt: string | null;
}

/** Request body for POST and PUT /api/device-maintenance (same shape; the API generates the maintenance id). */
interface MaintenanceRequest {
  deviceId: string;
  deviceName: string | null;
  model: string;
  assetRegistrationId: string | null;
  assetName: string | null;
  siteId: string | null;
  siteName: string;
  maintenanceType: MaintType;
  dueDate: string;
  assignedTo: string | null;
  status: ApiStatus;
  createdBy: string;
  clientId: string;
  tenantId: string;
}

/** The page's device kind (icon, plan, checklist) for a device name / model the API sends. */
export function kindOf(name: string, model: string): DeviceKind {
  const t = `${name} ${model}`.toLowerCase();
  if (t.includes('gateway') || t.includes('router')) return 'gateway';
  if (t.includes('vib') || /\bvx-/.test(t)) return 'vibration';
  if (t.includes('temp') || t.includes('humid')) return 'temp';
  if (t.includes('beacon') || t.includes('ble')) return 'beacon';
  if (t.includes('track') || t.includes('gps')) return 'tracker';
  if (t.includes('meter') || t.includes('energy')) return 'meter';
  return 'vibration';
}

/**
 * The device a job is for, from the job's own fields. The API keeps no health, uptime, firmware or warranty:
 * these show as unknown (uptime 0, firmware and warranty empty) unless the page already lists the device.
 */
export function deviceFromDto(dto: MaintenanceDto, known?: Device): Device {
  const asset = dto.assetRegistrationId ? { id: dto.assetRegistrationId, name: dto.assetName ?? dto.assetRegistrationId } : null;
  const name = dto.deviceName ?? known?.name ?? dto.deviceId;
  return {
    id: dto.deviceId,
    name,
    kind: known?.kind ?? kindOf(name, dto.model),
    model: dto.model,
    asset,
    infra: known?.infra ?? `IoT Device Fleet - ${dto.siteName}`,
    site: dto.siteName,
    siteId: dto.siteId ?? undefined,
    health: known?.health ?? 'Healthy',
    uptime: known?.uptime ?? 0,
    firmware: known?.firmware ?? '',
    warranty: known?.warranty ?? '',
  };
}

/** A page task for an API job: Completed / In Progress / Cancelled set the stage; the other statuses are planned. */
export function taskFromDto(dto: MaintenanceDto, kind: DeviceKind): Task {
  const items = PLANS.find((p) => p.kind === kind)?.checklist.length ?? 3;
  const status = dto.status ?? 'Scheduled';
  const stage = status === 'Completed' ? 'completed' : status === 'In Progress' ? 'in-progress' : status === 'Cancelled' ? 'cancelled' : 'planned';
  const assignee = dto.assignedTo ?? 'Unassigned';
  return {
    id: dto.maintenanceId,
    deviceId: dto.deviceId,
    type: dto.maintenanceType,
    due: dto.dueDate.slice(0, 10),
    assignee,
    // "Scheduled" means a slot is booked; "Due Soon" means it is not yet
    booked: status !== 'Due Soon' && status !== 'Overdue' && assignee !== 'Unassigned',
    stage,
    checklist: new Array(items).fill(stage === 'completed'),
    completedOn: stage === 'completed' ? (dto.updatedAt ?? dto.dueDate).slice(0, 10) : undefined,
  };
}

@Injectable({ providedIn: 'root' })
export class MaintenanceApiService {
  private apiUrl = `${environment.trackingApiUrl}api/device-maintenance`;

  constructor(private http: HttpClient, private auth: AuthService) {}

  /** GET /api/device-maintenance */
  getAll(): Observable<MaintenanceDto[]> {
    return this.http.get<MaintenanceDto[]>(this.apiUrl).pipe(catchError((e) => apiError(e, 'Could not load maintenance jobs.')));
  }

  /** GET /api/device-maintenance/{maintenanceId} */
  getById(maintenanceId: string): Observable<MaintenanceDto> {
    return this.http.get<MaintenanceDto>(`${this.apiUrl}/${encodeURIComponent(maintenanceId)}`).pipe(catchError((e) => apiError(e, 'Could not load the maintenance job.')));
  }

  /** POST /api/device-maintenance */
  create(t: Task, d: Device, status: ApiStatus): Observable<MaintenanceDto> {
    return this.http.post<MaintenanceDto>(this.apiUrl, this.request(t, d, status)).pipe(catchError((e) => apiError(e, `Could not schedule maintenance for ${d.id}.`)));
  }

  /** PUT /api/device-maintenance/{maintenanceId} (full replace) */
  update(t: Task, d: Device, status: ApiStatus): Observable<MaintenanceDto> {
    return this.http.put<MaintenanceDto>(`${this.apiUrl}/${encodeURIComponent(t.id)}`, this.request(t, d, status)).pipe(catchError((e) => apiError(e, `Could not save ${t.id}.`)));
  }

  /** DELETE /api/device-maintenance/{maintenanceId} */
  delete(maintenanceId: string): Observable<void> {
    return this.http.delete<void>(`${this.apiUrl}/${encodeURIComponent(maintenanceId)}`).pipe(catchError((e) => apiError(e, `Could not delete ${maintenanceId}.`)));
  }

  private request(t: Task, d: Device, status: ApiStatus): MaintenanceRequest {
    return {
      deviceId: d.id,
      deviceName: d.name || null,
      model: d.model,
      assetRegistrationId: d.asset?.id ?? null,
      assetName: d.asset?.name ?? null,
      siteId: d.siteId ?? null,
      siteName: d.site,
      maintenanceType: t.type,
      // the API keeps the date only, as UTC midnight
      dueDate: `${t.due}T00:00:00Z`,
      assignedTo: t.assignee === 'Unassigned' ? null : t.assignee,
      status,
      createdBy: this.auth.getUserEmail() || 'unknown',
      clientId: environment.clientId,
      tenantId: environment.tenantId,
    };
  }
}
