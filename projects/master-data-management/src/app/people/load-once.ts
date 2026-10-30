import { Observable, finalize, of, shareReplay, tap } from 'rxjs';

/**
 * Wraps a service's list load so pages can reload it on open (`refresh`) while pickers and lookups on
 * other pages only load it the first time (`once`). Calls made while a load is running share it.
 */
export class LoadOnce<T> {
  private loaded = false;
  private pending?: Observable<T>;

  constructor(private readonly loader: () => Observable<T>) {}

  refresh(): Observable<T> {
    const call = this.loader().pipe(
      tap(() => (this.loaded = true)),
      finalize(() => {
        if (this.pending === call) this.pending = undefined;
      }),
      shareReplay(1)
    );
    this.pending = call;
    return call;
  }

  once(): Observable<unknown> {
    return this.loaded ? of(null) : (this.pending ?? this.refresh());
  }

  /** Starts `once()` without waiting; a failed load is retried next time. */
  ensure(): void {
    this.once().subscribe({ error: () => undefined });
  }
}
