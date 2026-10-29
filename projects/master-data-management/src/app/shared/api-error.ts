import { HttpErrorResponse } from '@angular/common/http';

/** Pulls the message out of the API's 409 / validation-problem / 400 bodies. */
export function apiErrorMessage(err: unknown): string {
  if (err instanceof HttpErrorResponse) {
    if (err.status === 0) return 'Cannot reach the server. Check that the API gateway is running.';
    if (err.status === 401) return 'Your session is not authorised to call the API.';
    const body = err.error;
    if (body?.errors) return Object.values(body.errors as Record<string, string[]>).flat().join(' ');
    if (body?.message) return body.message;
  }
  return 'Something went wrong. Please try again.';
}
