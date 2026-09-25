import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { environment } from '../../../environments/environment';
import { FeaturesService } from '../../core/services/features.service';
import { PermissionService } from '../../core/services/permission.service';
import { MenuService } from './menu.service';

describe('MenuService', () => {
  let http: HttpTestingController;
  let menu: MenuService;

  async function signInWith(permissions: string[], features: string[] = ['Files']): Promise<void> {
    const loading = Promise.all([TestBed.inject(PermissionService).loadAsync(), TestBed.inject(FeaturesService).loadAsync()]);
    http.expectOne(`${environment.apiBaseUrl}/session/permissions`).flush({ data: permissions });
    http.expectOne(`${environment.apiBaseUrl}/features/enabled`).flush({ data: features });
    await loading;
  }

  const labels = () =>
    menu.visibleItems().flatMap((item) => (item.children ? item.children.map((c) => c.label) : [item.label]));

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    http = TestBed.inject(HttpTestingController);
    menu = TestBed.inject(MenuService);
  });

  it('shows only the entries the user is permitted to see (FR-SHELL-002)', async () => {
    await signInWith(['Users.View']);

    expect(labels()).toEqual(['Dashboard', 'Files', 'My exports', 'Users']);
  });

  it('hides a whole group when none of its entries are visible', async () => {
    await signInWith([]);

    expect(menu.visibleItems().some((item) => item.children)).toBe(false);
    expect(labels()).toEqual(['Dashboard', 'Files', 'My exports']);
  });

  it('hides an entry whose feature flag is off', async () => {
    await signInWith([], []);

    expect(labels()).toEqual(['Dashboard']);
  });

  it('shows the administration group with its permitted children only', async () => {
    await signInWith(['Users.View', 'Audit.View', 'Plugins.View']);

    const group = menu.visibleItems().find((item) => item.children)!;
    expect(group.label).toBe('Administration');
    expect(group.children!.map((c) => c.label)).toEqual(['Users', 'Audit Log', 'Plugins']);
  });

  it('adds a contribution and replaces it as a unit, so a disabled plugin leaves without a reload (FR-SHELL-008)', async () => {
    await signInWith([]);

    menu.replaceSource('plugins', [{ label: 'Tasks', icon: 'checklist', route: '/tasks' }]);
    expect(labels()).toContain('Tasks');

    menu.replaceSource('plugins', []);
    expect(labels()).not.toContain('Tasks');
    expect(labels()).toEqual(['Dashboard', 'Files', 'My exports']);
  });

  it('applies the same permission filter to contributed entries as to built-in ones', async () => {
    await signInWith([]);

    menu.replaceSource('plugins', [{ label: 'Tasks', icon: 'checklist', route: '/tasks', permission: 'Tasks.View' }]);
    expect(labels()).not.toContain('Tasks');

    await signInWith(['Tasks.View']);
    expect(labels()).toContain('Tasks');
  });
});
