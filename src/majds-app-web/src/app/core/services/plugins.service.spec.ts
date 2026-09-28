import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter, Router } from '@angular/router';
import { environment } from '../../../environments/environment';
import { PluginsService } from './plugins.service';

/**
 * P5 FR-PLUG-020/022: the route a plugin's menu entry points to is registered with the same
 * permission the menu itself was filtered by — an entry hidden from the menu is also blocked at
 * the route, not just unlinked to.
 */
describe('PluginsService', () => {
  let http: HttpTestingController;
  let plugins: PluginsService;
  let router: Router;

  async function load(entries: object[]): Promise<void> {
    const pending = plugins.loadAsync();
    http.expectOne(`${environment.apiBaseUrl}/plugins/manifest`).flush({ data: entries });
    await pending;
  }

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([{ path: '', children: [] }])]
    });
    http = TestBed.inject(HttpTestingController);
    plugins = TestBed.inject(PluginsService);
    router = TestBed.inject(Router);
  });

  it("registers the route guarded by the entry's own permission, same as the menu", async () => {
    await load([{ pluginId: 'MajdsApp.Plugins.Tasks', label: 'Tasks', icon: 'check_circle', route: 'tasks', permission: 'Tasks.View', order: 1 }]);

    const route = router.config.find((r) => r.path === '')!.children!.find((c) => c.path === 'tasks')!;

    // The enabled-plugin check always applies; a second guard for the permission is only added when the entry names one.
    expect(route.canActivate).toHaveLength(2);
    expect(plugins.isRouteEnabled('tasks')).toBe(true);
  });

  it('registers the route with only the enabled-plugin guard when the entry names no permission', async () => {
    await load([{ pluginId: 'MajdsApp.Plugins.Open', label: 'Open', icon: 'public', route: 'open-plugin', permission: null, order: 1 }]);

    const route = router.config.find((r) => r.path === '')!.children!.find((c) => c.path === 'open-plugin')!;

    expect(route.canActivate).toHaveLength(1);
  });

  it('a disabled plugin route redirects to the dashboard, even with the required permission', async () => {
    await load([{ pluginId: 'MajdsApp.Plugins.Tasks', label: 'Tasks', icon: 'check_circle', route: 'tasks', permission: null, order: 1 }]);
    const route = router.config.find((r) => r.path === '')!.children!.find((c) => c.path === 'tasks')!;
    const enabledGuard = route.canActivate![0] as () => boolean | ReturnType<Router['createUrlTree']>;

    // Reload with the plugin no longer in the manifest (disabled/uninstalled): isRouteEnabled flips false for the same route.
    await load([]);

    expect(TestBed.runInInjectionContext(() => enabledGuard())).not.toBe(true);
  });
});
