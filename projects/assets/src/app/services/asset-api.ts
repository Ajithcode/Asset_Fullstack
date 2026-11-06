import { HttpErrorResponse } from '@angular/common/http';
import { environment } from 'shared-ui';

/** Base URL of the Asset service API, e.g. `${ASSET_API}asset-categories`. */
export const ASSET_API = `${environment.assetApiUrl}api/`;

/** Media service: POST `${MEDIA_API}upload` (multipart `file` + `category`). */
export const MEDIA_API = `${environment.mediaApiUrl}api/media/`;

/** EventLog service: activity history, GET `${EVENTLOG_API}?entityId=...`. */
export const EVENTLOG_API = `${environment.eventLogApiUrl}api/eventlogs`;

/** Tenant and client stamped on every create request. */
export const TENANT = { tenantId: environment.tenantId, clientId: environment.clientId };

/** Readable message from an API error (ProblemDetails, validation errors or plain text). */
export function apiErrorMessage(err: unknown, fallback = 'Request failed. Please try again.'): string {
  if (!(err instanceof HttpErrorResponse)) return fallback;
  if (err.status === 0) return 'Cannot reach the Asset API. Check that the backend is running.';
  const body = err.error;
  if (typeof body === 'string' && body.trim()) return body;
  if (body && typeof body === 'object') {
    const errors = (body as { errors?: Record<string, string[] | string> }).errors;
    if (errors && typeof errors === 'object') {
      const first = Object.values(errors).flat()[0];
      if (first) return String(first);
    }
    const detail = (body as { detail?: string; title?: string; message?: string });
    if (detail.message) return detail.message;
    if (detail.detail) return detail.detail;
    if (detail.title) return detail.title;
  }
  return fallback;
}
