import { Injectable, inject, signal } from '@angular/core';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Observable, catchError, concatMap, from, map, of, tap, toArray } from 'rxjs';
import { environment } from 'shared-ui';
import { CURRENT_USER, todayIso } from '../shared/admin-common';
import { MODULE_ICONS, NumberSequence, ResetRule, SequenceScope, bumpVersion, cloneSequence } from './numbering-sequence.model';

/** Request body for POST and PUT {administrationApiUrl}api/administration/numbering-sequences[/{sequenceId}] */
interface SaveNumberingSequenceRequest {
  sequenceName: string;
  sequenceCode: string;
  module: string;
  recordType: string;
  prefix: string;
  separator: string;
  numberLength: number;
  startNumber: number;
  incrementBy: number;
  resetFrequency: ResetRule;
  scope: SequenceScope;
  status: 'Active' | 'Inactive';
}

/** Raw shape returned by the numbering sequences API. */
interface NumberingSequenceDto {
  sequenceId: string;
  sequenceName: string;
  sequenceCode: string;
  module: string;
  recordType: string;
  prefix: string;
  separator: string;
  numberLength: number;
  startNumber: number;
  incrementBy: number;
  resetFrequency: string;
  scope: string;
  status: string | null;
  createdBy: string | null;
  createdAt: string | null;
  updatedBy: string | null;
  updatedAt: string | null;
  tenantId: string | null;
  clientId: string | null;
}

/** The API needs a prefix; formats without a Prefix token fall back to the letters and digits of the code. */
function prefixOf(seq: NumberSequence): string {
  const token = seq.tokens.find((t) => t.kind === 'prefix')?.value.trim();
  return token || seq.code.split('-')[0].replace(/[^A-Za-z0-9]/g, '');
}

function toRequest(seq: NumberSequence): SaveNumberingSequenceRequest {
  return {
    sequenceName: seq.name.trim(),
    sequenceCode: seq.code.trim().toUpperCase(),
    module: seq.module,
    recordType: seq.recordType,
    prefix: prefixOf(seq),
    separator: seq.separator,
    numberLength: seq.numberLength,
    startNumber: seq.startNumber,
    incrementBy: seq.incrementBy,
    resetFrequency: seq.resetRule,
    scope: seq.scope,
    status: seq.active ? 'Active' : 'Inactive'
  };
}

/**
 * Starting point for a sequence loaded from the API with no local copy: the format is Prefix + Number and the fields
 * that have no backend field yet start at their defaults.
 */
function baseSequence(dto: NumberingSequenceDto): NumberSequence {
  const createdOn = (dto.createdAt ?? new Date().toISOString()).slice(0, 10);
  return {
    id: dto.sequenceId,
    name: dto.sequenceName,
    code: dto.sequenceCode,
    module: dto.module,
    recordType: dto.recordType,
    icon: MODULE_ICONS[dto.module] ?? 'box',
    tone: 'purple',
    description: '',
    tokens: [
      { kind: 'prefix', value: dto.prefix },
      { kind: 'number', value: '' }
    ],
    separator: dto.separator,
    numberLength: dto.numberLength,
    startNumber: dto.startNumber,
    currentNumber: dto.startNumber,
    incrementBy: dto.incrementBy,
    scope: dto.scope as SequenceScope,
    resetRule: dto.resetFrequency as ResetRule,
    resetDate: '',
    duplicatePrevention: true,
    manualOverride: false,
    active: dto.status !== 'Inactive',
    version: 'v1.0',
    createdOn,
    updatedOn: createdOn,
    usage: [],
    changes: [{ date: createdOn, version: 'v1.0', user: dto.createdBy ?? '', change: 'Created sequence' }]
  };
}

/**
 * The API stores the core settings only; the format tokens (date / location / suffix), current number, reset date,
 * icon, description, toggles, version, usage and change log have no backend field yet and are kept from the local
 * copy (for this session only).
 */
function toAppSequence(dto: NumberingSequenceDto, local: NumberSequence): NumberSequence {
  const copy = cloneSequence(local);
  const prefix = copy.tokens.find((t) => t.kind === 'prefix');
  if (prefix) prefix.value = dto.prefix;
  return {
    ...copy,
    id: dto.sequenceId,
    name: dto.sequenceName,
    code: dto.sequenceCode,
    module: dto.module,
    recordType: dto.recordType,
    separator: dto.separator,
    numberLength: dto.numberLength,
    startNumber: dto.startNumber,
    currentNumber: Math.max(copy.currentNumber, dto.startNumber),
    incrementBy: dto.incrementBy,
    resetRule: dto.resetFrequency as ResetRule,
    scope: dto.scope as SequenceScope,
    active: dto.status !== 'Inactive',
    createdOn: (dto.createdAt ?? new Date().toISOString()).slice(0, 10),
    updatedOn: (dto.updatedAt ?? dto.createdAt ?? new Date().toISOString()).slice(0, 10)
  };
}

/** First readable message from a ValidationProblem / Conflict / network error. */
export function apiErrorMessage(err: unknown): string {
  if (err instanceof HttpErrorResponse) {
    const body = err.error;
    if (body?.errors) {
      const first = Object.values(body.errors as Record<string, string[]>)[0];
      if (first?.length) return first[0];
    }
    if (typeof body?.message === 'string') return body.message;
    if (err.status === 0) return 'Could not reach the Administration API.';
  }
  return 'Something went wrong while talking to the Administration API.';
}

/** Numbering sequences backed by the Administration API (list, create, update, soft delete). */
@Injectable({ providedIn: 'root' })
export class NumberingSequenceService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.administrationApiUrl}api/administration/numbering-sequences`;

  private readonly store = signal<NumberSequence[]>([]);
  readonly sequences = this.store.asReadonly();

  get(id: string): NumberSequence | undefined {
    return this.store().find((s) => s.id === id);
  }

  codeTaken(code: string, exceptId: string | null): boolean {
    const c = code.trim().toUpperCase();
    return this.store().some((s) => s.id !== exceptId && s.code.toUpperCase() === c);
  }

  /** Loads the stored sequences, keeping the local-only fields of sequences already in memory. */
  load(): Observable<NumberSequence[]> {
    return this.http.get<NumberingSequenceDto[]>(this.baseUrl).pipe(
      map((dtos) => dtos.map((dto) => toAppSequence(dto, this.get(dto.sequenceId) ?? baseSequence(dto)))),
      tap((loaded) => this.store.set(loaded))
    );
  }

  /** POSTs a new sequence and adds the stored record to the top of the list. */
  create(draft: NumberSequence, change: string): Observable<NumberSequence> {
    return this.http.post<NumberingSequenceDto>(this.baseUrl, toRequest(draft)).pipe(
      map((dto) => {
        const saved = toAppSequence(dto, draft);
        saved.version = 'v1.0';
        saved.changes = [{ date: saved.createdOn, version: saved.version, user: dto.createdBy || CURRENT_USER.name, change }];
        return saved;
      }),
      tap((saved) => this.store.update((list) => [saved, ...list]))
    );
  }

  /** PUTs a stored sequence; logs the change and optionally bumps the version (publish). */
  update(draft: NumberSequence, change: string, publish = false): Observable<NumberSequence> {
    const copy = cloneSequence(draft);
    copy.code = copy.code.trim().toUpperCase();
    copy.name = copy.name.trim();
    if (publish) copy.version = bumpVersion(copy.version);
    return this.http.put<NumberingSequenceDto>(`${this.baseUrl}/${encodeURIComponent(copy.id)}`, toRequest(copy)).pipe(
      map((dto) => {
        const saved = toAppSequence(dto, copy);
        saved.changes = [{ date: todayIso(), version: saved.version, user: dto.updatedBy || CURRENT_USER.name, change }, ...copy.changes];
        return saved;
      }),
      tap((saved) => this.store.update((list) => list.map((s) => (s.id === saved.id ? saved : s))))
    );
  }

  /** Soft-deletes a sequence on the server and drops it from the list. */
  remove(id: string): Observable<void> {
    return this.http
      .delete<void>(`${this.baseUrl}/${encodeURIComponent(id)}`)
      .pipe(tap(() => this.store.update((list) => list.filter((s) => s.id !== id))));
  }

  /** Copy with a unique "-COPY" code; returned unsaved so the user can review it first. */
  cloneOf(source: NumberSequence): NumberSequence {
    const copy = cloneSequence(source);
    copy.id = '';
    copy.name = `${source.name} (Copy)`;
    let code = `${source.code}-COPY`.slice(0, 20);
    let n = 2;
    while (this.codeTaken(code, null)) code = `${source.code}-C${n++}`.slice(0, 20);
    copy.code = code;
    copy.currentNumber = copy.startNumber;
    copy.usage = [];
    copy.changes = [];
    return copy;
  }

  /** Creates imported sequences one by one, skipping codes that already exist. Emits how many were added. */
  importMany(rows: NumberSequence[]): Observable<number> {
    const fresh = rows.filter((r, i) => r.code && !this.codeTaken(r.code, null) && rows.findIndex((x) => x.code === r.code) === i);
    if (!fresh.length) return of(0);
    return from(fresh).pipe(
      concatMap((row) =>
        this.create({ ...row, id: '' }, 'Imported sequence').pipe(
          map(() => 1),
          catchError(() => of(0))
        )
      ),
      toArray(),
      map((added) => added.reduce((a, b) => a + b, 0))
    );
  }
}
