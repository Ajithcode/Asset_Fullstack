import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable, catchError, forkJoin, map, of, shareReplay, switchMap } from 'rxjs';
import { AuthService } from 'shared-ui';
import { ASSET_API, TENANT, apiErrorMessage } from '../services/asset-api';
import {
  ALL_ASSETS,
  CompoundPart,
  CustomField,
  FieldGroup,
  FieldLayout,
  FieldScope,
  FieldStatus,
  FieldType,
  builtInField,
} from './custom-fields.model';

// ===== API contracts (System.Text.Json camelCase) =====

type ApiScope = 'Asset' | 'Component' | 'Category';
type ApiFieldType = 'Text' | 'TextArea' | 'Number' | 'Date' | 'Dropdown' | 'Checkbox' | 'Compound' | 'Lookup';

interface PagedResponse<T> {
  items: T[];
  totalCount: number;
  page: number;
  pageSize: number;
}

export interface CustomFieldPartDto {
  key: string | null;
  label: string | null;
  unit: string | null;
}

/** AssetCustomFieldResponse */
export interface CustomFieldDto {
  id: string;
  scope: ApiScope;
  fieldName: string;
  displayLabel: string;
  fieldType: ApiFieldType;
  groupId: string | null;
  groupName: string | null;
  displayOrder: number;
  appliesToCategoryId: string | null;
  appliesToCategoryName: string | null;
  componentType: string | null;
  required: boolean;
  showInDetails: boolean;
  includeInSearch: boolean;
  status: string | null;
  defaultValue: string | null;
  description: string | null;
  helpText: string | null;
  options: string[];
  compoundParts: CustomFieldPartDto[];
  lookupSource: string | null;
  createdBy: string | null;
  createdAt: string | null;
  updatedBy: string | null;
  updatedAt: string | null;
}

/** SaveAssetCustomFieldRequest (create and update). */
export interface SaveCustomFieldRequest {
  scope: ApiScope;
  fieldName: string;
  displayLabel: string;
  fieldType: ApiFieldType;
  groupId: string | null;
  appliesToCategoryId: string | null;
  componentType: string | null;
  required: boolean;
  showInDetails: boolean;
  includeInSearch: boolean;
  status: FieldStatus;
  defaultValue: string | null;
  description: string | null;
  helpText: string | null;
  options: string[];
  compoundParts: CustomFieldPartDto[];
  lookupSource: string | null;
  createdBy?: string | null;
  clientId?: string | null;
  tenantId?: string | null;
}

/** AssetCustomFieldExportItem: the file format of field export / import. */
export interface CustomFieldExportItem {
  scope: string | null;
  fieldName: string | null;
  displayLabel: string | null;
  fieldType: string | null;
  groupName: string | null;
  appliesToCategoryCode: string | null;
  componentType: string | null;
  required: boolean | null;
  showInDetails: boolean | null;
  includeInSearch: boolean | null;
  status: string | null;
  defaultValue: string | null;
  description: string | null;
  helpText: string | null;
  options: string[] | null;
  compoundParts: CustomFieldPartDto[] | null;
  lookupSource: string | null;
}

export interface ImportItemError {
  name: string | null;
  message: string;
}

export interface ImportResult {
  created: number;
  updated: number;
  errors: ImportItemError[];
}

/** AssetFieldGroupResponse */
export interface FieldGroupDto {
  id: string;
  groupName: string;
  description: string | null;
  status: string | null;
  fieldCount: number;
  fields: { id: string; fieldName: string; displayLabel: string; fieldType: string; scope: string; displayOrder: number }[];
  createdBy: string | null;
  createdAt: string | null;
  updatedBy: string | null;
  updatedAt: string | null;
}

/** SaveAssetFieldGroupRequest; fieldIds null keeps the current assignment. */
export interface SaveFieldGroupRequest {
  groupName: string;
  description: string | null;
  status: FieldStatus;
  fieldIds: string[] | null;
  createdBy?: string | null;
  clientId?: string | null;
  tenantId?: string | null;
}

/** AssetFieldGroupExportItem */
export interface FieldGroupExportItem {
  groupName: string | null;
  description: string | null;
  status: string | null;
  fieldNames: string[] | null;
}

/** AvailableLayoutFieldResponse; source is "BuiltIn" or the custom field's scope. */
export interface AvailableLayoutFieldDto {
  fieldName: string;
  displayLabel: string;
  fieldType: ApiFieldType;
  required: boolean;
  source: string;
}

/** AssetFieldLayoutResponse */
export interface FieldLayoutDto {
  id: string;
  layoutName: string;
  assetTypeId: string | null;
  assetTypeName: string;
  description: string | null;
  status: string | null;
  groupCount: number;
  fieldCount: number;
  sections: { groupId: string; groupName: string | null; fields: AvailableLayoutFieldDto[] }[];
  createdBy: string | null;
  createdAt: string | null;
  updatedBy: string | null;
  updatedAt: string | null;
}

/** SaveAssetFieldLayoutRequest */
export interface SaveFieldLayoutRequest {
  layoutName: string;
  assetTypeId: string | null;
  description: string | null;
  status: FieldStatus;
  sections: { groupId: string; fieldNames: string[] }[];
  createdBy?: string | null;
  clientId?: string | null;
  tenantId?: string | null;
}

/** The parts of AssetCategoryResponse used here. */
export interface CategoryOption {
  id: string;
  parentCategoryId: string | null;
  name: string;
  code: string;
}

/** The parts of AssetCategoryTypeResponse used here. */
export interface AssetTypeOption {
  id: string;
  categoryId: string;
  typeName: string;
}

// ===== Mapping =====

const SCOPE_TO_API: Record<FieldScope, ApiScope> = { asset: 'Asset', component: 'Component', category: 'Category' };

export function toApiScope(scope: FieldScope): ApiScope {
  return SCOPE_TO_API[scope];
}

export function toApiFieldType(type: FieldType): ApiFieldType {
  return type === 'Text Area' ? 'TextArea' : type;
}

export function fromApiFieldType(type: string): FieldType {
  return type === 'TextArea' ? 'Text Area' : (type as FieldType);
}

function toStatus(status: string | null | undefined): FieldStatus {
  return status === 'Inactive' ? 'Inactive' : 'Active';
}

export function toCustomField(dto: CustomFieldDto): CustomField {
  const scope = (dto.scope?.toLowerCase() ?? 'asset') as FieldScope;
  const partDefs: CompoundPart[] = (dto.compoundParts ?? []).map((p) => ({ key: p.key ?? '', label: p.label || p.key || '', unit: p.unit ?? '' }));
  return {
    id: dto.id,
    scope,
    appliesTo: scope === 'category' ? dto.appliesToCategoryName ?? '' : scope === 'component' ? dto.componentType ?? '' : '',
    appliesToId: dto.appliesToCategoryId ?? '',
    name: dto.fieldName,
    label: dto.displayLabel,
    type: fromApiFieldType(dto.fieldType),
    group: dto.groupName ?? '',
    groupId: dto.groupId ?? '',
    required: dto.required,
    showInDetails: dto.showInDetails,
    searchable: dto.includeInSearch,
    defaultValue: dto.defaultValue ?? '',
    options: dto.options ?? [],
    parts: partDefs.map((p) => p.label),
    partDefs,
    lookupSource: dto.lookupSource ?? '',
    description: dto.description ?? '',
    tooltip: dto.helpText ?? '',
    status: toStatus(dto.status),
    order: dto.displayOrder ?? 0,
  };
}

/** "Length (mm)" → "length_mm"; never empty. */
function partKey(label: string): string {
  return label.toLowerCase().replace(/[^a-z0-9]+/g, '_').replace(/^_+|_+$/g, '') || 'part';
}

/** Sub-field definitions for the given labels, keeping the keys and units of labels that already existed. */
export function toCompoundParts(labels: string[], existing: CompoundPart[]): CustomFieldPartDto[] {
  const used = new Set<string>();
  return labels.map((label) => {
    const prior = existing.find((p) => p.label === label && !used.has(p.key));
    let key = prior?.key || partKey(label);
    for (let n = 2; used.has(key); n++) key = `${partKey(label)}_${n}`;
    used.add(key);
    return { key, label, unit: prior?.unit || null };
  });
}

/** Save request for a field as it stands in the UI model. */
export function toFieldRequest(f: CustomField): SaveCustomFieldRequest {
  const blank = (v: string) => (v.trim() ? v.trim() : null);
  return {
    scope: toApiScope(f.scope),
    fieldName: f.name,
    displayLabel: f.label,
    fieldType: toApiFieldType(f.type),
    groupId: f.scope === 'component' ? null : blank(f.groupId),
    appliesToCategoryId: f.scope === 'category' ? blank(f.appliesToId) : null,
    componentType: f.scope === 'component' ? blank(f.appliesTo) : null,
    required: f.required,
    showInDetails: f.showInDetails,
    includeInSearch: f.searchable,
    status: f.status,
    defaultValue: f.type === 'Compound' ? null : blank(f.defaultValue),
    description: blank(f.description),
    helpText: blank(f.tooltip),
    options: f.type === 'Dropdown' ? f.options : [],
    compoundParts: f.type === 'Compound' ? toCompoundParts(f.parts, f.partDefs) : [],
    lookupSource: f.type === 'Lookup' ? blank(f.lookupSource) : null,
  };
}

export function toFieldGroup(dto: FieldGroupDto, order: number): FieldGroup {
  return { id: dto.id, name: dto.groupName, description: dto.description ?? '', status: toStatus(dto.status), order };
}

/**
 * Layout for the UI. The API places fields by name; the UI by id, so names are resolved against the
 * built-in and asset/category fields (component fields can't be on a layout).
 */
export function toFieldLayout(dto: FieldLayoutDto, fields: CustomField[]): FieldLayout {
  const idByName = new Map(fields.filter((f) => f.scope !== 'component').map((f) => [f.name, f.id]));
  return {
    id: dto.id,
    name: dto.layoutName,
    assetTypeId: dto.assetTypeId ?? '',
    assetType: dto.assetTypeId ? dto.assetTypeName : ALL_ASSETS,
    description: dto.description ?? '',
    status: toStatus(dto.status),
    sections: (dto.sections ?? []).map((s) => ({
      groupId: s.groupId,
      group: s.groupName ?? '',
      fieldIds: s.fields.map((f) => idByName.get(f.fieldName)).filter((id): id is string => !!id),
    })),
  };
}

export function toLayoutRequest(l: FieldLayout, fields: CustomField[]): SaveFieldLayoutRequest {
  const nameById = new Map(fields.map((f) => [f.id, f.name]));
  return {
    layoutName: l.name,
    assetTypeId: l.assetTypeId || null,
    description: l.description.trim() || null,
    status: l.status,
    sections: l.sections.map((s) => ({
      groupId: s.groupId,
      fieldNames: s.fieldIds.map((id) => nameById.get(id)).filter((n): n is string => !!n),
    })),
  };
}

/** Outcome of running one request per item: what succeeded and the error messages of what failed. */
export interface BatchResult<T> {
  ok: T[];
  errors: string[];
}

export function runAll<I, T>(items: I[], call: (item: I) => Observable<T>): Observable<BatchResult<T>> {
  if (!items.length) return of({ ok: [], errors: [] });
  return forkJoin(
    items.map((item) =>
      call(item).pipe(
        map((value) => ({ value, error: null as string | null })),
        catchError((err) => of({ value: null as T | null, error: apiErrorMessage(err) })),
      ),
    ),
  ).pipe(
    map((results) => ({
      ok: results.filter((r) => r.error === null).map((r) => r.value as T),
      errors: results.filter((r) => r.error !== null).map((r) => r.error as string),
    })),
  );
}

const PAGE_SIZE = 200;

@Injectable({ providedIn: 'root' })
export class CustomFieldsService {
  private readonly http = inject(HttpClient);
  private readonly auth = inject(AuthService);

  private readonly fieldsUrl = `${ASSET_API}asset-custom-fields`;
  private readonly groupsUrl = `${ASSET_API}asset-field-groups`;
  private readonly layoutsUrl = `${ASSET_API}asset-field-layouts`;

  private categories$?: Observable<CategoryOption[]>;
  private assetTypes$?: Observable<AssetTypeOption[]>;

  /** Stamp for create requests. */
  private createStamp(): { createdBy: string; clientId: string; tenantId: string } {
    return { createdBy: this.auth.getUserEmail() || 'unknown', ...TENANT };
  }

  /** Loads every page of a paged list endpoint. */
  private fetchAll<T>(url: string, params: Record<string, string> = {}): Observable<T[]> {
    const pageOf = (page: number) =>
      this.http.get<PagedResponse<T>>(url, { params: new HttpParams({ fromObject: { ...params, page, pageSize: PAGE_SIZE } }) });
    return pageOf(1).pipe(
      switchMap((first) => {
        const pages = Math.ceil((first.totalCount ?? 0) / PAGE_SIZE);
        if (pages <= 1) return of(first.items ?? []);
        const rest = Array.from({ length: pages - 1 }, (_, i) => pageOf(i + 2));
        return forkJoin(rest).pipe(map((more) => [first, ...more].flatMap((r) => r.items ?? [])));
      }),
    );
  }

  // ----- Custom fields -----

  listFields(): Observable<CustomField[]> {
    return this.fetchAll<CustomFieldDto>(this.fieldsUrl).pipe(map((items) => items.map(toCustomField)));
  }

  createField(body: SaveCustomFieldRequest): Observable<CustomField> {
    return this.http.post<CustomFieldDto>(this.fieldsUrl, { ...body, ...this.createStamp() }).pipe(map(toCustomField));
  }

  updateField(id: string, body: SaveCustomFieldRequest): Observable<CustomField> {
    return this.http.put<CustomFieldDto>(`${this.fieldsUrl}/${encodeURIComponent(id)}`, body).pipe(map(toCustomField));
  }

  deleteField(id: string): Observable<void> {
    return this.http.delete<void>(`${this.fieldsUrl}/${encodeURIComponent(id)}`);
  }

  exportFields(scope?: FieldScope): Observable<CustomFieldExportItem[]> {
    const params = scope ? new HttpParams().set('scope', toApiScope(scope)) : undefined;
    return this.http.get<CustomFieldExportItem[]>(`${this.fieldsUrl}/export`, { params });
  }

  /** Items without a scope get `scope`. */
  importFields(items: unknown[], scope?: FieldScope): Observable<ImportResult> {
    const params = scope ? new HttpParams().set('scope', toApiScope(scope)) : undefined;
    return this.http.post<ImportResult>(`${this.fieldsUrl}/import`, items, { params });
  }

  // ----- Field groups -----

  listGroups(): Observable<FieldGroup[]> {
    return this.fetchAll<FieldGroupDto>(this.groupsUrl).pipe(map((items) => items.map(toFieldGroup)));
  }

  createGroup(body: SaveFieldGroupRequest): Observable<FieldGroupDto> {
    return this.http.post<FieldGroupDto>(this.groupsUrl, { ...body, ...this.createStamp() });
  }

  updateGroup(id: string, body: SaveFieldGroupRequest): Observable<FieldGroupDto> {
    return this.http.put<FieldGroupDto>(`${this.groupsUrl}/${encodeURIComponent(id)}`, body);
  }

  deleteGroup(id: string): Observable<void> {
    return this.http.delete<void>(`${this.groupsUrl}/${encodeURIComponent(id)}`);
  }

  exportGroups(): Observable<FieldGroupExportItem[]> {
    return this.http.get<FieldGroupExportItem[]>(`${this.groupsUrl}/export`);
  }

  importGroups(items: unknown[]): Observable<ImportResult> {
    return this.http.post<ImportResult>(`${this.groupsUrl}/import`, items);
  }

  // ----- Field layouts -----

  listLayouts(): Observable<FieldLayoutDto[]> {
    return this.fetchAll<FieldLayoutDto>(this.layoutsUrl);
  }

  createLayout(body: SaveFieldLayoutRequest): Observable<FieldLayoutDto> {
    return this.http.post<FieldLayoutDto>(this.layoutsUrl, { ...body, ...this.createStamp() });
  }

  updateLayout(id: string, body: SaveFieldLayoutRequest): Observable<FieldLayoutDto> {
    return this.http.put<FieldLayoutDto>(`${this.layoutsUrl}/${encodeURIComponent(id)}`, body);
  }

  deleteLayout(id: string): Observable<void> {
    return this.http.delete<void>(`${this.layoutsUrl}/${encodeURIComponent(id)}`);
  }

  /** The standard asset fields every layout can use (GET available-fields without an asset type, built-ins only). */
  builtInFields(): Observable<CustomField[]> {
    return this.http.get<AvailableLayoutFieldDto[]>(`${this.layoutsUrl}/available-fields`).pipe(
      map((items) =>
        items
          .filter((f) => f.source === 'BuiltIn')
          .map((f) => builtInField(f.fieldName, f.displayLabel, fromApiFieldType(f.fieldType), f.required)),
      ),
    );
  }

  // ----- Lookups (cached for the session) -----

  categories(): Observable<CategoryOption[]> {
    this.categories$ ??= this.http.get<CategoryOption[]>(`${ASSET_API}asset-categories`).pipe(
      map((items) => items.map((c) => ({ id: c.id, parentCategoryId: c.parentCategoryId ?? null, name: c.name, code: c.code }))),
      shareReplay({ bufferSize: 1, refCount: false }),
    );
    return this.categories$;
  }

  assetTypes(): Observable<AssetTypeOption[]> {
    this.assetTypes$ ??= this.http.get<AssetTypeOption[]>(`${ASSET_API}asset-category-types`).pipe(
      map((items) => items.map((t) => ({ id: t.id, categoryId: t.categoryId, typeName: t.typeName }))),
      shareReplay({ bufferSize: 1, refCount: false }),
    );
    return this.assetTypes$;
  }
}
