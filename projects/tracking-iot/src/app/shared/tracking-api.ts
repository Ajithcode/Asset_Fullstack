import { HttpErrorResponse, HttpParams } from '@angular/common/http';
import { Observable, throwError } from 'rxjs';
import { environment } from 'shared-ui';

/** Full URL of a Tracking service resource, e.g. trackingUrl('tag-ids') → {gateway}api/tag-ids. */
export function trackingUrl(resource: string): string {
  return `${environment.trackingApiUrl}api/${resource}`;
}

/** clientId / tenantId every Tracking call is scoped to, plus any extra query values (empty ones are skipped). */
export function tenantParams(extra: Record<string, string | number | boolean | null | undefined> = {}): HttpParams {
  let params = new HttpParams().set('clientId', environment.clientId).set('tenantId', environment.tenantId);
  for (const [key, value] of Object.entries(extra)) {
    if (value !== null && value !== undefined && value !== '') params = params.set(key, String(value));
  }
  return params;
}

/** Fields every create / update request carries. */
export function tenantBody(): { clientId: string; tenantId: string } {
  return { clientId: environment.clientId, tenantId: environment.tenantId };
}

/** Readable message for an API failure: { message }, ASP.NET validation { errors }, or a network failure. */
export function apiErrorMessage(err: unknown, fallback: string): string {
  if (!(err instanceof HttpErrorResponse)) return err instanceof Error && err.message ? err.message : fallback;
  if (err.status === 0) return 'Cannot reach the server. Check that the backend is running.';
  const body = err.error;
  const firstError = body?.errors && typeof body.errors === 'object' ? Object.values(body.errors).flat()[0] : undefined;
  if (typeof firstError === 'string') return firstError;
  if (typeof body?.message === 'string') return body.message;
  if (typeof body?.title === 'string') return body.title;
  return fallback;
}

/** catchError handler that rethrows the failure as an Error with a readable message. */
export function apiError(err: unknown, fallback: string): Observable<never> {
  return throwError(() => new Error(apiErrorMessage(err, fallback)));
}
