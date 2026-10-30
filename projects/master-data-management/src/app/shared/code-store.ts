import { inject, signal } from '@angular/core';
import { Observable, finalize, map, of, tap } from 'rxjs';
import { MdmApi, apiErrorMessage, inSequence } from './mdm-api';

/**
 * In-memory list of records keyed by `code`, shared by the simpler master services.
 * API-backed masters extend `ApiCodeStore` below.
 */
export abstract class CodeStore<T extends { code: string; name?: string }> {
  protected records: T[];

  protected constructor(initial: T[]) {
    this.records = initial;
  }

  all(): T[] {
    return this.records;
  }

  find(code: string | null | undefined): T | undefined {
    return code ? this.records.find((r) => r.code === code) : undefined;
  }

  findByName(name: string): T | undefined {
    return this.records.find((r) => r.name === name);
  }

  isCodeTaken(code: string, exceptCode: string | null): boolean {
    const wanted = code.trim().toLowerCase();
    return this.records.some((r) => r.code.toLowerCase() === wanted && r.code !== exceptCode);
  }

  isNameTaken(name: string, exceptCode: string | null): boolean {
    const wanted = name.trim().toLowerCase();
    return this.records.some((r) => (r.name ?? '').toLowerCase() === wanted && r.code !== exceptCode);
  }

  /** Replaces the record that had `originalCode`, or adds a new one at the end. */
  save(record: T, originalCode: string | null): void {
    const index = originalCode ? this.records.findIndex((r) => r.code === originalCode) : -1;
    if (index >= 0) {
      this.records[index] = record;
      this.records = [...this.records];
    } else {
      this.records = [...this.records, record];
    }
  }

  remove(codes: string[]): void {
    this.records = this.records.filter((r) => !codes.includes(r.code));
  }
}

/**
 * CodeStore backed by `/api/master-data/{resource}`. Records stay keyed by `code` for the pages; the
 * Mongo id of each one is kept alongside. `load()` fills the list; `persist()` / `delete()` call the API
 * and update the local list only when the call succeeds.
 */
export abstract class ApiCodeStore<T extends { code: string; name?: string }, TDto extends { id: string }> extends CodeStore<T> {
  protected readonly api = inject(MdmApi);
  private readonly ids = new Map<string, string>();
  readonly loading = signal(false);
  readonly error = signal<string | null>(null);

  /** Path under /api/master-data/, e.g. 'failure-codes'. */
  protected abstract readonly resource: string;
  protected abstract toRecord(dto: TDto): T;
  /** Body for POST / PUT (the stamp fields are added by MdmApi). */
  protected abstract toRequest(record: T): object;

  protected constructor() {
    super([]);
  }

  idOf(code: string): string | undefined {
    return this.ids.get(code);
  }

  load(query: Record<string, string | number | boolean | null | undefined> = {}): Observable<T[]> {
    this.loading.set(true);
    this.error.set(null);
    return this.api.list<TDto>(this.resource, query).pipe(
      map((dtos) => {
        this.ids.clear();
        this.records = dtos.map((dto) => this.adopt(dto));
        return this.records;
      }),
      tap({ error: (err) => this.error.set(apiErrorMessage(err)) }),
      finalize(() => this.loading.set(false))
    );
  }

  /** Creates (no `originalCode`) or updates the record, then stores what the server returned. */
  persist(record: T, originalCode: string | null): Observable<T> {
    const id = originalCode ? this.ids.get(originalCode) : undefined;
    const call = id
      ? this.api.update<TDto>(this.resource, id, this.toRequest(record))
      : this.api.create<TDto>(this.resource, this.toRequest(record));
    return call.pipe(
      map((dto) => {
        if (originalCode) this.ids.delete(originalCode);
        const saved = this.adopt(dto);
        this.save(saved, id ? originalCode : null);
        return saved;
      })
    );
  }

  /** Deletes the records one at a time; codes deleted before a failure are still removed locally. */
  delete(codes: string[]): Observable<unknown> {
    const calls = codes.map((code) => {
      const id = this.ids.get(code);
      const call = id ? this.api.remove(this.resource, id) : of(null);
      return call.pipe(tap(() => this.forget(code)));
    });
    return inSequence(calls);
  }

  private forget(code: string): void {
    this.ids.delete(code);
    this.remove([code]);
  }

  private adopt(dto: TDto): T {
    const record = this.toRecord(dto);
    this.ids.set(record.code, dto.id);
    return record;
  }
}
