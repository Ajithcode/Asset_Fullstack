import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, catchError, forkJoin, map, of, switchMap, throwError } from 'rxjs';
import { AuthService } from 'shared-ui';
import { ASSET_API, TENANT, apiErrorMessage } from '../services/asset-api';
import { AssetListItemDto, AssetLookupService, AssetRegistrationDto } from '../asset-maintenance/asset-lookup.service';
import { MAINTENANCE_API, WorkOrderDto } from '../asset-maintenance/maintenance-api.service';

// ===== Asset API DTOs (camelCase JSON of the *Response records) =====

/** GET /api/asset-assignments item (AssetAssignmentResponse); only what the lifecycle reads. */
export interface AssignmentDto {
  id: string;
  assignmentNumber: string;
  status: string | null;
  assignTo: string;
  assignedToName: string | null;
  fromDate: string | null;
  toDate: string | null;
  location: { siteName: string | null; buildingName: string | null; roomName: string | null } | null;
  purpose: string | null;
  createdBy: string | null;
  createdAt: string | null;
}

/** GET /api/asset-condition-assessments item. */
export interface ConditionAssessmentDto {
  id: string;
  assessmentDate: string;
  assessedBy: string;
  overallCondition: string;
  healthScore: number;
  remarks: string | null;
}

/** GET /api/asset-disposals item (AssetDisposalRequestListItemResponse). */
export interface DisposalDto {
  id: string;
  requestNumber: string;
  requestTitle: string;
  requestDate: string | null;
  requestedBy: string | null;
  disposalMethod: string | null;
  expectedDisposalDate: string | null;
  status: string | null;
  firstAsset: { assetRegistrationId: string; assetTag: string | null } | null;
}

/** GET /api/service-requests item. */
export interface ServiceRequestItemDto {
  id: string;
  requestNumber: string;
  title: string;
  status: string | null;
  requestType: string | null;
  requestedDate: string | null;
  requestedBy: string | null;
}

/** GET /api/asset-lifecycle/{assetRegistrationId} (AssetLifecycleResponse). */
export interface LifecycleRecordDto {
  assetRegistrationId: string;
  stages: { key: string; status: string; date: string | null; owner: string | null; notes: string | null }[];
  events: { eventId: string; date: string; title: string; by: string | null; kind: string }[];
}

export interface LifecycleLoad {
  item: AssetListItemDto;
  registration: AssetRegistrationDto;
  assignments: AssignmentDto[];
  assessments: ConditionAssessmentDto[];
  disposals: DisposalDto[];
  serviceRequests: ServiceRequestItemDto[];
  workOrders: WorkOrderDto[];
  /** AMC contracts of the asset (Maintenance service). */
  amc: { vendorName: string; endDate: string | null; isDeleted?: boolean }[];
  /** Null when the lifecycle endpoint is not available. */
  lifecycle: LifecycleRecordDto | null;
  /** Sources that could not be read. */
  errors: string[];
}

/** Asset Lifecycle: read from the registration and the asset's other records; stage changes and events in /api/asset-lifecycle. */
@Injectable({ providedIn: 'root' })
export class AssetLifecycleService {
  private readonly http = inject(HttpClient);
  private readonly auth = inject(AuthService);
  private readonly lookup = inject(AssetLookupService);

  get user(): string {
    return this.auth.getUser()?.userName || this.auth.getUserEmail() || '';
  }

  load(tag: string): Observable<LifecycleLoad> {
    return this.lookup.byTag(tag).pipe(
      switchMap((item) => {
        if (!item) return throwError(() => new Error(`Asset ${tag} is not registered.`));
        const id = item.assetRegistrationId;
        const errors: string[] = [];
        const list = <T>(label: string, url: string, params: Record<string, string | number>): Observable<T[]> =>
          this.http.get<{ items: T[] }>(url, { params: { page: 1, pageSize: 200, ...params } }).pipe(
            map((r) => r.items ?? []),
            catchError((err) => {
              errors.push(`${label}: ${apiErrorMessage(err)}`);
              return of([] as T[]);
            }),
          );
        return forkJoin({
          registration: this.lookup.registration(id),
          assignments: list<AssignmentDto>('Assignments', `${ASSET_API}asset-assignments`, { assetRegistrationId: id }),
          assessments: list<ConditionAssessmentDto>('Condition assessments', `${ASSET_API}asset-condition-assessments`, { assetRegistrationId: id }),
          disposals: list<DisposalDto>('Disposals', `${ASSET_API}asset-disposals`, { search: tag }),
          serviceRequests: list<ServiceRequestItemDto>('Service requests', `${ASSET_API}service-requests`, { assetRegistrationId: id }),
          workOrders: this.http.get<WorkOrderDto[]>(`${MAINTENANCE_API}work-orders/asset/${encodeURIComponent(tag)}`).pipe(
            catchError((err) => {
              errors.push(`Work orders: ${apiErrorMessage(err)}`);
              return of([] as WorkOrderDto[]);
            }),
          ),
          amc: this.http
            .get<LifecycleLoad['amc']>(`${MAINTENANCE_API}vendor-amc/asset/${encodeURIComponent(tag)}`)
            .pipe(catchError(() => of([] as LifecycleLoad['amc']))),
          lifecycle: this.http.get<LifecycleRecordDto>(`${ASSET_API}asset-lifecycle/${encodeURIComponent(id)}`).pipe(catchError(() => of(null))),
        }).pipe(
          map((r) => ({
            ...r,
            item,
            disposals: r.disposals.filter((d) => (d.firstAsset?.assetTag ?? '').toUpperCase() === tag.toUpperCase() || d.firstAsset?.assetRegistrationId === id),
            workOrders: r.workOrders.filter((w) => !w.isDeleted),
            amc: r.amc.filter((a) => !a.isDeleted),
            errors,
          })),
        );
      }),
    );
  }

  /** Edit Stage / Move to Next Stage. */
  saveStage(assetRegistrationId: string, key: string, s: { status: string; date: string; owner: string; notes: string; completePrevious?: string }): Observable<LifecycleRecordDto> {
    return this.http.put<LifecycleRecordDto>(`${ASSET_API}asset-lifecycle/${encodeURIComponent(assetRegistrationId)}/stages/${key}`, {
      status: s.status, date: s.date || null, owner: s.owner, notes: s.notes, completePrevious: s.completePrevious ?? null,
    });
  }

  addEvent(assetRegistrationId: string, e: { date: string; title: string; by: string; kind: string }): Observable<LifecycleRecordDto> {
    return this.http.post<LifecycleRecordDto>(`${ASSET_API}asset-lifecycle/${encodeURIComponent(assetRegistrationId)}/events`, e);
  }

  /** Record Condition: a new condition assessment in the Asset API. */
  addAssessment(assetRegistrationId: string, a: { date: string; condition: string; healthScore: number; remarks: string }): Observable<ConditionAssessmentDto> {
    return this.http.post<ConditionAssessmentDto>(`${ASSET_API}asset-condition-assessments`, {
      assetRegistrationId, assessmentDate: a.date, assessedBy: this.user || null, overallCondition: a.condition, healthScore: a.healthScore,
      remarks: a.remarks || null, createdBy: this.user || null, ...TENANT,
    });
  }
}
