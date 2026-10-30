import { Observable, map, shareReplay, tap } from 'rxjs';
import { ApiCodeStore } from '../shared/code-store';
import { toLocalStamp } from './structure-api';

interface StructureRecord {
  code: string;
  name?: string;
  status: string;
  updatedOn: string;
  // Masters that nest (organizations, business units, departments, locations) point at their parent's code
  parentCode?: string | null;
}

/**
 * ApiCodeStore for the organization structure masters. Adds what they share: a status PATCH, a
 * one-off load other masters can wait on for their pickers, id -> code lookups, and parents that
 * the API gives by id but the pages keep by code (`toRecord` may put the parent's id in
 * `parentCode`; it is swapped for the code once the whole list is known).
 */
export abstract class StructureStore<T extends StructureRecord, TDto extends { id: string; status: string | null; updatedAt: string | null }> extends ApiCodeStore<T, TDto> {
  private firstLoad?: Observable<T[]>;

  override load(query: Record<string, string | number | boolean | null | undefined> = {}): Observable<T[]> {
    return super.load(query).pipe(map(() => this.resolveParents()));
  }

  /** Loads once and shares the result; used by pages that only need this master for lookups. */
  ensureLoaded(): Observable<T[]> {
    this.firstLoad ??= this.load().pipe(
      tap({ error: () => (this.firstLoad = undefined) }),
      shareReplay(1)
    );
    return this.firstLoad;
  }

  override persist(record: T, originalCode: string | null): Observable<T> {
    return super.persist(record, originalCode).pipe(
      map((saved) => {
        // Children keep pointing at a renamed parent
        if (originalCode && originalCode !== saved.code) {
          this.records = this.records.map((r) => (r.parentCode === originalCode ? { ...r, parentCode: saved.code } : r));
        }
        this.resolveParents();
        return this.find(saved.code) ?? saved;
      })
    );
  }

  setStatus(record: T, status: string): Observable<T> {
    return this.api.setStatus<TDto>(this.resource, this.idOf(record.code) ?? '', status).pipe(
      map((dto) => {
        const saved = { ...record, status: dto.status ?? status, updatedOn: toLocalStamp(dto.updatedAt) || record.updatedOn };
        this.save(saved, record.code);
        return saved;
      })
    );
  }

  /** Code of the record with this API id, or null when it isn't loaded. */
  codeOf(id: string | null | undefined): string | null {
    if (!id) return null;
    return this.records.find((r) => this.idOf(r.code) === id)?.code ?? null;
  }

  /** Request reference for the record with this code, or null. */
  refOf(code: string | null | undefined): { id: string; name: string | null } | null {
    const id = code ? this.idOf(code) : undefined;
    return id ? { id, name: this.find(code)?.name ?? null } : null;
  }

  /** Deletes nested records deepest first, so the API never sees a parent that still has children. */
  deleteNested(codes: string[]): Observable<unknown> {
    const depth = (code: string) => {
      let d = 0;
      for (let r = this.find(code); r?.parentCode && d < 50; r = this.find(r.parentCode)) d++;
      return d;
    };
    return this.delete([...codes].sort((a, b) => depth(b) - depth(a)));
  }

  private resolveParents(): T[] {
    const codes = new Set(this.records.map((r) => r.code));
    if (this.records.every((r) => !r.parentCode || codes.has(r.parentCode))) return this.records;
    const codeById = new Map(this.records.map((r) => [this.idOf(r.code), r.code]));
    this.records = this.records.map((r) =>
      r.parentCode && !codes.has(r.parentCode) ? { ...r, parentCode: codeById.get(r.parentCode) ?? null } : r
    );
    return this.records;
  }
}
