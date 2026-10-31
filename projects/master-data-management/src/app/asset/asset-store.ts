import { Observable, map, of, tap } from 'rxjs';
import { ApiCodeStore } from '../shared/code-store';
import { MdmAudit, inSequence } from '../shared/mdm-api';

/**
 * ApiCodeStore for the Asset masters. The pages read each other's lists (types read categories,
 * models read types and manufacturers, details count models), so a store loads once on first use
 * and its own page reloads it when opened.
 */
export abstract class AssetStore<T extends { code: string; name?: string }, TDto extends MdmAudit> extends ApiCodeStore<T, TDto> {
  private requested = false;

  /** Reading the list starts the first load, so pages that only look up this master need no setup. */
  override all(): T[] {
    this.ensureLoaded();
    return super.all();
  }

  /** Loads the list the first time anything needs it. */
  ensureLoaded(): void {
    if (this.requested) return;
    this.requested = true;
    this.load().subscribe({ error: () => (this.requested = false) });
  }

  /** Reloads the list; used by the store's own page when it opens. */
  reload(): Observable<T[]> {
    this.requested = true;
    return this.load().pipe(tap({ error: () => (this.requested = false) }));
  }

  /** PATCHes only the status, then stores what the server returned. */
  setStatus(record: T & { status: string }, status: string): Observable<T> {
    const id = this.idOf(record.code);
    if (!id) return of({ ...record, status });
    return this.api.setStatus<TDto>(this.resource, id, status).pipe(
      map((dto) => {
        const saved = this.toRecord(dto);
        this.save(saved, record.code);
        return saved;
      })
    );
  }

  /** Saves several records one after another, e.g. rules mirrored onto other records. */
  persistAll(records: T[]): Observable<T[]> {
    return inSequence(records.map((r) => this.persist(r, r.code)));
  }
}

/** API timestamp -> the local 'YYYY-MM-DDTHH:mm' the pages format. */
export function localStamp(iso: string | null | undefined): string {
  if (!iso) return '';
  const date = new Date(iso);
  if (Number.isNaN(date.getTime())) return '';
  return new Date(date.getTime() - date.getTimezoneOffset() * 60000).toISOString().slice(0, 16);
}

/** Active / Inactive from the API's free-text status. */
export function activeStatus(status: string | null | undefined): 'Active' | 'Inactive' {
  return (status ?? 'Active').toLowerCase() === 'inactive' ? 'Inactive' : 'Active';
}
