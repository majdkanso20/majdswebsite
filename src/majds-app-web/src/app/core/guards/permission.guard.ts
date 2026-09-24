import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { PermissionService } from '../services/permission.service';

/**
 * Route-level enforcement of a required permission (U3 FR-SHELL-006), reading from the same
 * PermissionService the nav and `hasPermission` directive use — one source of truth (AC-SHELL-1).
 * Usage: { path: 'users', canActivate: [permissionGuard('Users.View')], ... }
 */
export function permissionGuard(permission: string): CanActivateFn {
  return () => {
    const permissions = inject(PermissionService);
    const router = inject(Router);
    return permissions.has(permission) ? true : router.createUrlTree(['/dashboard']);
  };
}
