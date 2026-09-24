import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { MatSnackBar } from '@angular/material/snack-bar';
import { catchError, throwError } from 'rxjs';

/**
 * Central handling of failures that mean the same thing on every screen (FR-ERR-004): being refused,
 * being throttled, and the server being unreachable or broken. Field-level problems (400/404/409) are
 * left to the screen that made the call, which knows how to present them; 401 is handled by the
 * auth interceptor. The message comes from the ResponseDto envelope when the server sent one.
 */
export const errorInterceptor: HttpInterceptorFn = (req, next) => {
  const snackBar = inject(MatSnackBar);

  return next(req).pipe(
    catchError((error: HttpErrorResponse) => {
      const serverMessage = (error.error as { message?: string } | null)?.message;

      if (error.status === 403) {
        snackBar.open(serverMessage || 'You do not have permission to do that.', 'Dismiss', { duration: 5000 });
      } else if (error.status === 429) {
        snackBar.open('Too many requests. Please wait a moment and try again.', 'Dismiss', { duration: 6000 });
      } else if (error.status === 0) {
        snackBar.open('Cannot reach the server. Check your connection and try again.', 'Dismiss', { duration: 6000 });
      } else if (error.status >= 500) {
        snackBar.open('The server ran into a problem. Please try again.', 'Dismiss', { duration: 6000 });
      }

      return throwError(() => error);
    })
  );
};
