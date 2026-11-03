import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable, catchError, forkJoin, map, of, switchMap } from 'rxjs';
import { AuthService, environment } from 'shared-ui';
import { ASSET_API, TENANT } from '../services/asset-api';
import { BuildingService } from '../services/building.service';
import { CountryService } from '../services/country.service';

// ===== DTOs (System.Text.Json camelCase of Contracts/AssetAuditContracts.cs) =====

export interface AuditScopeDto {
  locationId: string | null;
  locationName: string | null;
  departmentId: string | null;
  departmentName: string | null;
  categoryId: string | null;
  categoryName: string | null;
  selectionMethod: string | null;
  tagFrom: string | null;
  tagTo: string | null;
  assetStatus: string | null;
  assetType: string | null;
  criticality: string | null;
  totalAssetsInScope?: number;
}

export interface AuditAssetDto {
  assetRegistrationId: string;
  assetName: string | null;
  assetTag: string | null;
  category: string | null;
  assetType: string | null;
  location: string | null;
  status: string | null;
  componentIds: string[];
}

export interface AuditCriterionDto {
  criterionId: string | null;
  name: string;
  description: string | null;
  category: string | null;
  type: string;
  checkMethod: string | null;
  appliesTo: string;
  assetRegistrationIds: string[];
  isMandatory: boolean;
}

export interface AuditCriteriaSettingsDto {
  capturePhotos: boolean;
  allowComments: boolean;
  defaultMandatory: boolean;
  showPassFail: boolean;
  enableScoring: boolean;
  allowAttachments: boolean;
}

export interface AuditTeamMemberDto {
  userId: string;
  name: string;
  email: string | null;
  role: string;
  department: string | null;
  assignedAssets?: number;
}

export interface AuditScheduleDto {
  startDate: string | null;
  endDate: string | null;
  workingDays: string | null;
  customDays: string[];
  dailyStartTime: string | null;
  dailyEndTime: string | null;
  locationSequence: string | null;
  autoPlan: boolean;
}

export interface AuditPlanEntryDto {
  date: string;
  location: string | null;
  assetRegistrationIds: string[];
  assignedToUserId: string | null;
  assignedToName?: string | null;
  status: string;
}

export interface AuditNotificationsDto {
  notifyTeamOnAssignment: boolean;
  sendScheduleReminder: boolean;
  notifyOnCompletion: boolean;
  notifyOnScheduleChange: boolean;
}

export interface AuditAttachmentDto {
  fileName: string;
  fileUrl: string;
  fileType: string | null;
  contentType: string | null;
  fileSize: number | null;
  uploadedBy: string | null;
  uploadedAt: string | null;
}

export interface AssetAuditDto {
  id: string;
  auditReference: string;
  status: string | null;
  currentStep: number;
  auditName: string;
  auditType: string | null;
  description: string | null;
  scope: AuditScopeDto;
  criteriaOptions: string[];
  otherCriteriaDetails: string | null;
  selectedAssetCount: number;
  assets: AuditAssetDto[];
  criteria: AuditCriterionDto[];
  criteriaSettings: AuditCriteriaSettingsDto;
  team: AuditTeamMemberDto[];
  schedule: AuditScheduleDto;
  plan: AuditPlanEntryDto[];
  notifications: AuditNotificationsDto;
  attachments: AuditAttachmentDto[];
  notes: string | null;
  submittedAt: string | null;
  startedAt: string | null;
  completedAt: string | null;
  discrepancyCount: number | null;
  completionRemarks: string | null;
}

export interface AuditListItemDto {
  id: string;
  auditReference: string;
  auditName: string;
  auditType: string | null;
  location: string | null;
  scheduledDate: string | null;
  endDate: string | null;
  completedDate: string | null;
  status: string | null;
  auditedBy: string | null;
  assetCount: number;
  discrepancyCount: number | null;
}

export interface AuditCounts {
  total: number;
  completed: number;
  inProgress: number;
  scheduled: number;
  overdue: number;
  draft: number;
}

interface AuditListDto {
  items: AuditListItemDto[];
  totalCount: number;
  counts: AuditCounts;
  page: number;
  pageSize: number;
}

export interface AuditOptionsDto {
  auditTypes: string[];
  statuses: string[];
  selectionMethods: string[];
  /** Key -> checkbox label, e.g. "PhysicalExistence" -> "Physical existence (asset present at location)". */
  criteriaOptions: Record<string, string>;
  criterionTypes: string[];
  checkMethods: string[];
  appliesTo: string[];
  roles: string[];
  workingDays: string[];
  daysOfWeek: string[];
  locationSequences: string[];
}

export interface CriterionTemplateDto {
  key: string;
  name: string;
  description: string;
  category: string;
  type: string;
  checkMethod: string;
  isMandatory: boolean;
  optionKey: string | null;
}

export interface AuditPlanResponseDto {
  plan: { date: string; location: string | null; assetsCount: number; assetRegistrationIds: string[]; assignedToUserId: string | null; assignedToName: string | null; status: string }[];
  team: AuditTeamMemberDto[];
  workingDayCount: number;
  totalAssets: number;
}

export interface AuditImportResponseDto {
  audit: AssetAuditDto;
  importedCount: number;
  notFound: string[];
}

// ===== Requests =====

export interface AuditBasicInformationRequest {
  auditName: string;
  auditType: string | null;
  auditReference: string | null;
  description: string | null;
  scope: Omit<AuditScopeDto, 'totalAssetsInScope'>;
  criteriaOptions: string[];
  otherCriteriaDetails: string | null;
}

export interface AuditCriteriaRequest {
  criteria: AuditCriterionDto[];
  settings: AuditCriteriaSettingsDto;
}

export interface AuditScheduleRequest {
  team: AuditTeamMemberDto[];
  schedule: AuditScheduleDto;
  plan: AuditPlanEntryDto[] | null;
  notifications: AuditNotificationsDto;
}

export interface AuditListQuery {
  search?: string;
  auditType?: string;
  status?: string;
  locationId?: string;
  fromDate?: string;
  toDate?: string;
}

// ===== Lookups =====

/** A location the scope can be set to; the id matches any level of an asset's location. */
export interface AuditLocation {
  id: string;
  name: string;
}

/** A person who can be added to the audit team (Administration users). */
export interface AuditUserOption {
  id: string;
  name: string;
  email: string;
  department: string;
  title: string;
}

/** An asset in the register (GET /api/asset-registrations). */
export interface RegisterAsset {
  id: string;
  name: string;
  tag: string;
  category: string;
  assetType: string;
  location: string;
  department: string;
  status: string;
  criticality: string;
}

export interface AssetSensorDto {
  id: string;
  componentName: string;
  componentTag: string;
  status: string | null;
}

interface AssetListItemDto {
  assetRegistrationId: string;
  assetName: string | null;
  assetTag: string | null;
  category: string | null;
  assetType: string | null;
  locationPath: string | null;
  siteName: string | null;
  buildingName: string | null;
  department: string | null;
  status: string | null;
  criticality: string | null;
}

interface AssetRegistrationListDto {
  items: AssetListItemDto[];
  totalCount: number;
}

interface UserDto {
  userId: string;
  name: string;
  email: string;
  department: string | null;
  jobTitle: string | null;
}

const PAGE = 200;
const MAX_PAGES = 25;

/** "YYYY-MM-DD" of an API date-time, or ''. */
export function apiDate(value: string | null | undefined): string {
  return value ? value.slice(0, 10) : '';
}

@Injectable({ providedIn: 'root' })
export class AssetAuditService {
  private readonly http = inject(HttpClient);
  private readonly auth = inject(AuthService);
  private readonly buildings = inject(BuildingService);
  private readonly countries = inject(CountryService);
  private readonly url = `${ASSET_API}asset-audits`;

  get userEmail(): string {
    return this.auth.getUserEmail() ?? '';
  }

  // ----- List page -----

  /** Every audit matching the filters (up to 5,000), with the summary counts. */
  list(query: AuditListQuery): Observable<{ items: AuditListItemDto[]; counts: AuditCounts; totalCount: number }> {
    const params = this.listParams(query);
    const page = (n: number) => this.http.get<AuditListDto>(this.url, { params: params.set('page', n).set('pageSize', PAGE) });
    return page(1).pipe(
      switchMap((first) => {
        const pages = Math.min(MAX_PAGES, Math.ceil(first.totalCount / PAGE));
        if (pages <= 1) return of({ items: first.items, counts: first.counts, totalCount: first.totalCount });
        return forkJoin(Array.from({ length: pages - 1 }, (_, i) => page(i + 2))).pipe(
          map((rest) => ({ items: [...first.items, ...rest.flatMap((r) => r.items)], counts: first.counts, totalCount: first.totalCount })),
        );
      }),
    );
  }

  /** GET /export: the list with the same filters, as an Excel file. */
  export(query: AuditListQuery): Observable<Blob> {
    return this.http.get(`${this.url}/export`, { params: this.listParams(query), responseType: 'blob' });
  }

  private listParams(query: AuditListQuery): HttpParams {
    let params = new HttpParams();
    for (const [key, value] of Object.entries(query)) if (value) params = params.set(key, value);
    return params;
  }

  get(id: string): Observable<AssetAuditDto> {
    return this.http.get<AssetAuditDto>(`${this.url}/${encodeURIComponent(id)}`);
  }

  delete(id: string): Observable<void> {
    return this.http.delete<void>(`${this.url}/${encodeURIComponent(id)}`);
  }

  bulkDelete(ids: string[]): Observable<{ deletedCount: number; skipped: string[] }> {
    return this.http.post<{ deletedCount: number; skipped: string[] }>(`${this.url}/bulk-delete`, { ids });
  }

  start(id: string): Observable<AssetAuditDto> {
    return this.http.post<AssetAuditDto>(`${this.url}/${encodeURIComponent(id)}/start`, {});
  }

  complete(id: string, discrepancyCount: number, remarks: string): Observable<AssetAuditDto> {
    return this.http.post<AssetAuditDto>(`${this.url}/${encodeURIComponent(id)}/complete`, { discrepancyCount, remarks: remarks || null });
  }

  // ----- Wizard -----

  options(): Observable<AuditOptionsDto> {
    return this.http.get<AuditOptionsDto>(`${this.url}/options`);
  }

  criteriaTemplates(): Observable<CriterionTemplateDto[]> {
    return this.http.get<CriterionTemplateDto[]>(`${this.url}/criteria-templates`);
  }

  /** Step 1 on a new audit: creates the draft. */
  create(body: AuditBasicInformationRequest, saveAsDraft: boolean): Observable<AssetAuditDto> {
    return this.http.post<AssetAuditDto>(this.url, { ...body, createdBy: this.userEmail, ...TENANT }, { params: { saveAsDraft } });
  }

  updateBasicInformation(id: string, body: AuditBasicInformationRequest, saveAsDraft: boolean): Observable<AssetAuditDto> {
    return this.http.put<AssetAuditDto>(`${this.url}/${encodeURIComponent(id)}/basic-information`, body, { params: { saveAsDraft } });
  }

  updateAssets(id: string, assets: { assetRegistrationId: string; componentIds: string[] }[], saveAsDraft: boolean): Observable<AssetAuditDto> {
    return this.http.put<AssetAuditDto>(`${this.url}/${encodeURIComponent(id)}/assets`, { assets }, { params: { saveAsDraft } });
  }

  importAssets(id: string, file: File): Observable<AuditImportResponseDto> {
    const form = new FormData();
    form.append('file', file, file.name);
    return this.http.post<AuditImportResponseDto>(`${this.url}/${encodeURIComponent(id)}/assets/import`, form);
  }

  updateCriteria(id: string, body: AuditCriteriaRequest, saveAsDraft: boolean): Observable<AssetAuditDto> {
    return this.http.put<AssetAuditDto>(`${this.url}/${encodeURIComponent(id)}/criteria`, body, { params: { saveAsDraft } });
  }

  updateSchedule(id: string, body: AuditScheduleRequest, saveAsDraft: boolean): Observable<AssetAuditDto> {
    return this.http.put<AssetAuditDto>(`${this.url}/${encodeURIComponent(id)}/schedule`, body, { params: { saveAsDraft } });
  }

  /** Regenerate Plan: the plan for the team and schedule on screen; nothing is saved. */
  generatePlan(id: string, body: AuditScheduleRequest): Observable<AuditPlanResponseDto> {
    return this.http.post<AuditPlanResponseDto>(`${this.url}/${encodeURIComponent(id)}/schedule/plan`, { ...body, plan: null });
  }

  updateReview(id: string, attachments: AuditAttachmentDto[], notes: string): Observable<AssetAuditDto> {
    return this.http.put<AssetAuditDto>(`${this.url}/${encodeURIComponent(id)}/review`, { attachments, notes: notes || null });
  }

  submit(id: string): Observable<AssetAuditDto> {
    return this.http.post<AssetAuditDto>(`${this.url}/${encodeURIComponent(id)}/submit`, {});
  }

  // ----- Lookups -----

  /** Countries and buildings from the Project API; an asset matches when any level of its location has the id. */
  locations(): Observable<AuditLocation[]> {
    return forkJoin({
      countries: this.countries.getAll().pipe(catchError(() => of([]))),
      buildings: this.buildings.getAll().pipe(catchError(() => of([]))),
    }).pipe(
      map(({ countries, buildings }) => [
        ...countries.map((c) => ({ id: c.id, name: c.countryName })),
        ...buildings.map((b) => ({ id: b.id, name: b.buildingName })),
      ].filter((l) => l.id && l.name)),
    );
  }

  /** Active asset category names. */
  categories(): Observable<string[]> {
    return this.http.get<{ name: string }[]>(`${ASSET_API}asset-categories`).pipe(
      map((list) => [...new Set(list.map((c) => c.name).filter(Boolean))].sort((a, b) => a.localeCompare(b))),
    );
  }

  /** Users of this tenant from the Administration API, for the audit team. */
  users(): Observable<AuditUserOption[]> {
    return this.http
      .get<UserDto[]>(`${environment.administrationApiUrl}api/users`, { params: { tenantId: TENANT.tenantId, clientId: TENANT.clientId } })
      .pipe(map((list) => list.map((u) => ({ id: u.userId, name: u.name, email: u.email, department: u.department ?? '', title: u.jobTitle ?? '' }))));
  }

  /** Registered assets (READ-only GET /api/asset-registrations), all pages up to 5,000. */
  registerAssets(filter: { locationId?: string; department?: string; category?: string }): Observable<RegisterAsset[]> {
    let params = new HttpParams().set('pageSize', PAGE);
    for (const [key, value] of Object.entries(filter)) if (value) params = params.set(key, value);
    const page = (n: number) => this.http.get<AssetRegistrationListDto>(`${ASSET_API}asset-registrations`, { params: params.set('page', n) });
    return page(1).pipe(
      switchMap((first) => {
        const pages = Math.min(MAX_PAGES, Math.ceil(first.totalCount / PAGE));
        if (pages <= 1) return of(first.items);
        return forkJoin(Array.from({ length: pages - 1 }, (_, i) => page(i + 2))).pipe(map((rest) => [...first.items, ...rest.flatMap((r) => r.items)]));
      }),
      map((items) => items.map(toRegisterAsset)),
    );
  }

  /** Sensors / components of an asset. */
  components(assetRegistrationId: string): Observable<AssetSensorDto[]> {
    return this.http
      .get<{ items: AssetSensorDto[] }>(`${ASSET_API}asset-registrations/${encodeURIComponent(assetRegistrationId)}/components`, { params: { pageSize: 200 } })
      .pipe(map((r) => r.items ?? []));
  }
}

function toRegisterAsset(dto: AssetListItemDto): RegisterAsset {
  return {
    id: dto.assetRegistrationId,
    name: dto.assetName ?? '',
    tag: dto.assetTag || dto.assetRegistrationId,
    category: dto.category ?? '',
    assetType: dto.assetType ?? '',
    location: dto.locationPath || dto.buildingName || dto.siteName || '',
    department: dto.department ?? '',
    status: dto.status ?? '',
    criticality: dto.criticality ?? '',
  };
}
