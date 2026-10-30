import { Observable, map } from 'rxjs';
import { ApiCodeStore } from '../shared/code-store';
import { LoadOnce } from '../people/load-once';
import { VendorBase, VendorBaseDto, VendorStatus } from './vendor.data';

/** API-backed list shared by the vendor services: loading on open, and status changes. */
export abstract class VendorStore<T extends VendorBase, TDto extends VendorBaseDto> extends ApiCodeStore<T, TDto> {
  private readonly loader = new LoadOnce(() => this.load());

  protected constructor() {
    super();
  }

  /** Reloads the list from the API. */
  refresh(): Observable<T[]> {
    return this.loader.refresh();
  }

  /** Loads the list once, for pickers that only read it. */
  ensureLoaded(): void {
    this.loader.ensure();
  }

  setStatus(record: T, status: VendorStatus): Observable<T> {
    return this.api.setStatus<TDto>(this.resource, this.idOf(record.code)!, status).pipe(
      map((dto) => {
        const saved = this.toRecord(dto);
        this.save(saved, record.code);
        return saved;
      })
    );
  }
}
