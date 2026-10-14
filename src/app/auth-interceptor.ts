import { HttpInterceptorFn } from '@angular/common/http';

// DEMO MODE: no token refresh; just attach the stored demo token if there is one
export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const token = typeof window !== 'undefined' ? localStorage.getItem('token') : null;   // guard SSR
  if (token) {
    req = req.clone({
      setHeaders: { Authorization: `Bearer ${token}` }
    });
  }
  return next(req);
};
