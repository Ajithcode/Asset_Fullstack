import { Observable, map, throwError } from 'rxjs';
import { ApiCodeStore } from '../shared/code-store';
import { MdmAudit, apiErrorMessage } from '../shared/mdm-api';

/** `yyyy-mm-ddThh:mm` from an API timestamp, as the pages show it. */
export function stampOf(value: string | null | undefined): string {
  return (value ?? '').slice(0, 16);
}

/** `yyyy-mm-dd` for date inputs, or null for an empty one (API DateOnly). */
export function dateOrNull(value: string | null | undefined): string | null {
  return value ? value.slice(0, 10) : null;
}

/**
 * ApiCodeStore for the Maintenance and Compliance masters: loads once on demand for the pages that
 * only read it (drop-downs, delete blockers), reloads when its own page opens, and patches status.
 */
export abstract class MasterApiStore<T extends { code: string; name: string }, TDto extends MdmAudit> extends ApiCodeStore<T, TDto> {
  private requested = false;

  /** Reloads the list; `onError` gets a readable message when it fails. */
  refresh(onError?: (message: string) => void): void {
    this.requested = true;
    this.load().subscribe({
      error: (err) => {
        this.requested = false;
        onError?.(apiErrorMessage(err));
      }
    });
  }

  /** Loads the list unless it is already loaded or on its way. */
  ensureLoaded(): void {
    if (!this.requested) this.refresh();
  }

  /** PATCH /{id}/status, then keeps what the server returned. */
  setStatus(code: string, status: string): Observable<T> {
    const id = this.idOf(code);
    if (!id) return throwError(() => new Error('Record not found.'));
    return this.api.setStatus<TDto>(this.resource, id, status).pipe(
      map((dto) => {
        const saved = this.toRecord(dto);
        this.save(saved, code);
        return saved;
      })
    );
  }

  /** Mongo id of the record with this name, for references sent as { id, name }. */
  idOfName(name: string): string | null {
    const record = this.findByName(name);
    return (record && this.idOf(record.code)) ?? null;
  }
}
