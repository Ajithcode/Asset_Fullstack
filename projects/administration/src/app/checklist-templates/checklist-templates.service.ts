import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Injectable, computed, inject, signal } from '@angular/core';
import { Observable, catchError, concat, forkJoin, map, of, tap, throwError, toArray } from 'rxjs';
import { AuthService, environment } from 'shared-ui';
import { ChecklistItem, ChecklistTemplate, ResponseType } from './checklist-templates.model';

/** Body of POST /api/administration/checklist-items */
interface CreateChecklistItemRequest {
  itemText: string;
  section: string;
  responseType: ResponseType;
  isRequired: boolean;
  isCritical: boolean;
  helpText: string | null;
  expectedValue: string | null;
  attachmentRequired: boolean;
  scoreValue: number;
  failureAction: string | null;
  escalateIssue: boolean;
  createdBy: string | null;
  tenantId: string;
  clientId: string;
}

/** Item fields shared by create and update; tenant, client and creator are immutable. */
type ChecklistItemBody = Omit<CreateChecklistItemRequest, 'createdBy' | 'tenantId' | 'clientId'>;

/** Body of PUT /api/administration/checklist-items/{itemId} */
type UpdateChecklistItemRequest = ChecklistItemBody & { updatedBy: string | null };

/** A persisted item with the section it was saved under (the API has no template, only a section name). */
export interface SavedChecklistItem {
  section: string;
  item: ChecklistItem;
}

/** Raw shape returned by the checklist-items API. */
interface ChecklistItemDto {
  itemId: string;
  itemText: string;
  section: string;
  responseType: string;
  isRequired: boolean;
  isCritical: boolean;
  helpText: string | null;
  expectedValue: string | null;
  attachmentRequired: boolean;
  scoreValue: number;
  failureAction: string | null;
  escalateIssue: boolean;
  createdBy: string | null;
  createdAt: string | null;
  updatedBy: string | null;
  updatedAt: string | null;
  tenantId: string | null;
  clientId: string | null;
}

/** A saved item with the audit fields the template row shows. */
interface SavedRow extends SavedChecklistItem {
  createdBy: string;
  updatedAt: string;
}

/** Template id = its section name, lower case (the API has no template record). */
export function templateKey(section: string): string {
  return section.trim().toLowerCase();
}

/** Stable CHK-... code from the template name (letters and digits only, up to 20 characters). */
function codeOf(name: string): string {
  const slug = name.toUpperCase().replace(/[^A-Z0-9]+/g, '-').replace(/^-|-$/g, '').slice(0, 15).replace(/-$/, '');
  return `CHK-${slug || 'GEN'}`.slice(0, 20);
}

function toAppItem(dto: ChecklistItemDto): ChecklistItem {
  return {
    id: dto.itemId,
    text: dto.itemText,
    responseType: dto.responseType as ResponseType,
    required: dto.isRequired,
    critical: dto.isCritical,
    score: dto.scoreValue,
    helpText: dto.helpText ?? '',
    expected: dto.expectedValue ?? '',
    attachmentRequired: dto.attachmentRequired,
    failureAction: dto.failureAction ?? 'None',
    escalate: dto.escalateIssue
  };
}

function itemBody(item: ChecklistItem, sectionTitle: string): ChecklistItemBody {
  return {
    itemText: item.text.trim(),
    section: sectionTitle.trim() || 'General',
    responseType: item.responseType,
    isRequired: item.required,
    isCritical: item.critical,
    helpText: item.helpText.trim() || null,
    expectedValue: item.expected.trim() || null,
    attachmentRequired: item.attachmentRequired,
    scoreValue: item.score,
    failureAction: item.failureAction && item.failureAction !== 'None' ? item.failureAction : null,
    escalateIssue: item.escalate
  };
}

function toSaved(dto: ChecklistItemDto): SavedRow {
  return { section: dto.section, item: toAppItem(dto), createdBy: dto.createdBy ?? '', updatedAt: dto.updatedAt ?? dto.createdAt ?? '' };
}

function errorMessage(err: HttpErrorResponse): string {
  const errors = err.error?.errors as Record<string, string[]> | undefined;
  const first = errors ? Object.values(errors)[0]?.[0] : null;
  return first || err.error?.message || err.error?.title || 'The checklist service could not be reached.';
}

/*
 * Checklist items are persisted through the Administration API (list, create, update, delete). The API has no template
 * record, so a template IS a section name: the template list is built from the saved items grouped by section (one
 * section per template, named like the template). Creating, renaming or deleting a template creates, updates or deletes
 * its items. Code, category, owner and status shown for a template are derived, not stored.
 */
@Injectable({ providedIn: 'root' })
export class ChecklistTemplatesService {
  private readonly http = inject(HttpClient);
  private readonly auth = inject(AuthService);
  private readonly itemsUrl = `${environment.administrationApiUrl}api/administration/checklist-items`;
  private idSeq = 1000;
  private readonly savedStore = signal<SavedRow[]>([]);
  /** Items persisted on the server, ordered by section. */
  readonly savedItems = this.savedStore.asReadonly();
  /** One template per section of the saved items. */
  readonly templates = computed<ChecklistTemplate[]>(() => {
    const groups = new Map<string, SavedRow[]>();
    for (const row of this.savedStore()) {
      const key = templateKey(row.section);
      if (!groups.has(key)) groups.set(key, []);
      groups.get(key)!.push(row);
    }
    return [...groups.entries()].map(([key, rows]) => this.toTemplate(key, rows));
  });

  private toTemplate(key: string, rows: SavedRow[]): ChecklistTemplate {
    const title = rows[0].section.trim();
    const updated = rows.map((r) => r.updatedAt).filter(Boolean).sort().pop() ?? '';
    return {
      id: key,
      name: title,
      code: codeOf(title),
      version: 'v1.0',
      category: 'Inspection',
      owner: rows[0].createdBy,
      updated: updated.slice(0, 10),
      status: 'Active',
      icon: 'clipboard',
      tone: 'purple',
      description: '',
      frequency: '',
      assetTypes: [],
      locations: [],
      teams: [],
      requireApproval: false,
      passThreshold: 80,
      scoringMethod: 'Weighted',
      failOnCritical: true,
      sections: [{ id: `S-${key}`, title, items: rows.map((r) => ({ ...r.item })) }],
      uses: 0,
      versions: [],
      executions: []
    };
  }

  /** GET /api/administration/checklist-items for the current tenant and client. */
  loadItems(): Observable<SavedChecklistItem[]> {
    const params = { tenantId: environment.tenantId, clientId: environment.clientId };
    return this.http.get<ChecklistItemDto[]>(this.itemsUrl, { params }).pipe(
      map((rows) => rows.map(toSaved)),
      tap((rows) => this.savedStore.set(rows)),
      catchError((err: HttpErrorResponse) => throwError(() => new Error(errorMessage(err))))
    );
  }

  /** True when the item has a server itemId, so edits and deletes go through the API. */
  isPersisted(id: string): boolean {
    return this.savedStore().some((s) => s.item.id === id);
  }

  get(id: string): ChecklistTemplate | undefined {
    return this.templates().find((t) => t.id === id);
  }

  /** Unique id for new sections / items */
  newId(prefix: string): string {
    this.idSeq += 1;
    return `${prefix}-${this.idSeq}`;
  }

  /** POST /api/administration/checklist-items; the returned item carries the server's itemId. */
  createItem(item: ChecklistItem, sectionTitle: string): Observable<ChecklistItem> {
    const request: CreateChecklistItemRequest = {
      ...itemBody(item, sectionTitle),
      createdBy: this.auth.getUserEmail(),
      tenantId: environment.tenantId,
      clientId: environment.clientId
    };
    return this.http.post<ChecklistItemDto>(this.itemsUrl, request).pipe(
      map(toSaved),
      tap((saved) => this.savedStore.update((list) => [...list, saved])),
      map((saved) => saved.item),
      catchError((err: HttpErrorResponse) => throwError(() => new Error(errorMessage(err))))
    );
  }

  /** PUT /api/administration/checklist-items/{itemId} for an item that has a server itemId. */
  updateItem(item: ChecklistItem, sectionTitle: string): Observable<ChecklistItem> {
    const request: UpdateChecklistItemRequest = { ...itemBody(item, sectionTitle), updatedBy: this.auth.getUserEmail() };
    return this.http.put<ChecklistItemDto>(`${this.itemsUrl}/${encodeURIComponent(item.id)}`, request).pipe(
      map(toSaved),
      tap((saved) => this.savedStore.update((list) => list.map((s) => (s.item.id === saved.item.id ? saved : s)))),
      map((saved) => saved.item),
      catchError((err: HttpErrorResponse) => throwError(() => new Error(errorMessage(err))))
    );
  }

  /** DELETE /api/administration/checklist-items/{itemId} (soft delete on the server). */
  deleteItem(id: string): Observable<void> {
    return this.http.delete<void>(`${this.itemsUrl}/${encodeURIComponent(id)}`).pipe(
      tap(() => this.savedStore.update((list) => list.filter((s) => s.item.id !== id))),
      catchError((err: HttpErrorResponse) => throwError(() => new Error(errorMessage(err))))
    );
  }

  /** True when another template already uses this name (names are the section names, compared ignoring case). */
  nameTaken(name: string, exceptId: string | null): boolean {
    const key = templateKey(name);
    return this.templates().some((t) => t.id !== exceptId && t.id === key);
  }

  /** Creates a new template: POSTs every item of its section under the template name, one after the other. */
  createTemplate(name: string, items: ChecklistItem[]): Observable<unknown> {
    return items.length ? concat(...items.map((i) => this.createItem({ ...i }, name))).pipe(toArray()) : of(null);
  }

  /** Renames a template: every item moves to the new section name. */
  renameTemplate(id: string, name: string): Observable<unknown> {
    const rows = this.savedStore().filter((r) => templateKey(r.section) === id);
    return rows.length ? forkJoin(rows.map((r) => this.updateItem(r.item, name))) : of(null);
  }

  /** Deletes a template: every item of its section is deleted. */
  deleteTemplate(id: string): Observable<unknown> {
    const rows = this.savedStore().filter((r) => templateKey(r.section) === id);
    return rows.length ? forkJoin(rows.map((r) => this.deleteItem(r.item.id))) : of(null);
  }

  /** Copy as a new unsaved template with fresh item ids; the items are created when it is saved. */
  cloneOf(source: ChecklistTemplate, name = `${source.name} (Copy)`): ChecklistTemplate {
    const copy = structuredClone(source);
    copy.id = '';
    copy.name = name;
    copy.code = codeOf(name);
    copy.owner = '';
    copy.sections.forEach((s) => {
      s.id = this.newId('S');
      s.title = name;
      s.items.forEach((i) => (i.id = this.newId('I')));
    });
    return copy;
  }
}
