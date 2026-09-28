import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { provideRouter } from '@angular/router';
import { PermissionService } from '../services/permission.service';
import { permissionGuard } from './permission.guard';

/**
 * U3 FR-SHELL-006 / P5 FR-PLUG-022: the same check `*hasPermission` and the nav use, applied to a
 * route — a plugin's dynamic route is guarded with this, given the same permission its menu entry
 * carries (see PluginsService), so an entry hidden from the menu is also blocked at the route.
 */
describe('permissionGuard', () => {
  function create(has: boolean) {
    TestBed.configureTestingModule({
      providers: [provideRouter([]), { provide: PermissionService, useValue: { has: () => has } }]
    });
    return TestBed.runInInjectionContext(() => permissionGuard('Tasks.View')(null!, null!));
  }

  it('lets the route activate when the user has the permission', () => {
    expect(create(true)).toBe(true);
  });

  it('redirects to the dashboard instead of activating when the user lacks the permission', () => {
    const result = create(false);

    expect(result).not.toBe(true);
    expect(TestBed.inject(Router).serializeUrl(result as ReturnType<Router['createUrlTree']>)).toBe('/dashboard');
  });
});
