import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { Injector, inject } from '@angular/core';
import { MatSnackBar } from '@angular/material/snack-bar';
import { catchError, throwError } from 'rxjs';

/**
 * Central handling of failures that mean the same thing on every screen (FR-ERR-004): being refused,
 * being throttled, and the server being unreachable or broken. Field-level problems (400/404/409) are
 * left to the screen that made the call, which knows how to present them; 401 is handled by the
 * auth interceptor. The message comes from the ResponseDto envelope when the server sent one.
 *
 * The snack bar is looked up only when there is an error to show, not when the interceptor is created: the snack bar needs the language service,
 * and the language service fetches its translation file (through this interceptor) while it is being constructed, so asking for the snack bar
 * up front made the two wait for each other and blanked the whole app whenever a language other than English was remembered.
 */
export const errorInterceptor: HttpInterceptorFn = (req, next) => {
  const injector = inject(Injector);

  return next(req).pipe(
    catchError((error: HttpErrorResponse) => {
      const snackBar = () => injector.get(MatSnackBar);
      const serverMessage = (error.error as { message?: string } | null)?.message;

      if (error.status === 403) {
        snackBar().open(serverMessage || 'You do not have permission to do that.', 'Dismiss', { duration: 5000 });
      } else if (error.status === 429) {
        snackBar().open('Too many requests. Please wait a moment and try again.', 'Dismiss', { duration: 6000 });
      } else if (error.status === 0) {
        snackBar().open('Cannot reach the server. Check your connection and try again.', 'Dismiss', { duration: 6000 });
      } else if (error.status >= 500) {
        snackBar().open('The server ran into a problem. Please try again.', 'Dismiss', { duration: 6000 });
      }

      return throwError(() => error);
    })
  );
};
