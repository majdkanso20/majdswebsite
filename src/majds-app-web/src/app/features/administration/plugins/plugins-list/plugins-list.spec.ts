import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { MatDialog } from '@angular/material/dialog';
import { of } from 'rxjs';
import { PermissionService } from '../../../../core/services/permission.service';
import { PluginsService } from '../../../../core/services/plugins.service';
import { InstalledPluginDto, PendingPluginChangeDto, PluginsApiService } from '../plugins-api.service';
import { PluginsList } from './plugins-list';

const plugin = (over: Partial<InstalledPluginDto> = {}): InstalledPluginDto => ({
  id: 'Acme.Tasks', name: 'Tasks', version: '1.0.0', author: 'Acme', isEnabled: true, lastError: null, discoveredAt: '2026-09-25T00:00:00',
  permissions: ['Tasks.View', 'Tasks.Edit'], menuEntries: ['Tasks'], canRollback: false, pendingUninstall: false,
  state: 'Enabled', diagnostics: [{ check: 'Platform version', ok: true, detail: 'fine' }], settingsGroup: null,
  minHostVersion: null, maxHostVersion: null, ...over
});

describe('PluginsList', () => {
  const api = { list: vi.fn(), pending: vi.fn(), setEnabled: vi.fn(), uninstall: vi.fn(), rollback: vi.fn(), cancelPending: vi.fn(), install: vi.fn() };
  const pluginsService = { loadAsync: vi.fn() };
  const dialog = { open: vi.fn() };
  let canManage = true;

  async function create(plugins: InstalledPluginDto[], pending: PendingPluginChangeDto[] = []) {
    api.list.mockReturnValue(of(plugins));
    api.pending.mockReturnValue(of(pending));
    const fixture = TestBed.createComponent(PluginsList);
    await fixture.whenStable();
    fixture.detectChanges();
    return { fixture, page: fixture.componentInstance, element: fixture.nativeElement as HTMLElement };
  }

  beforeEach(() => {
    Object.values(api).forEach((fn) => fn.mockReset());
    pluginsService.loadAsync.mockReset().mockResolvedValue(undefined);
    dialog.open.mockReset();
    vi.spyOn(window, 'confirm').mockReturnValue(true);
    canManage = true;
    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        { provide: PluginsApiService, useValue: api },
        { provide: PluginsService, useValue: pluginsService },
        { provide: PermissionService, useValue: { has: () => canManage } }
      ]
    });
    TestBed.overrideProvider(MatDialog, { useValue: dialog });
  });

  it('shows what each plugin declares: its permissions, menu entries and a way to assign them to roles', async () => {
    const { element } = await create([plugin()]);

    expect(element.textContent).toContain('Tasks.View');
    expect(element.textContent).toContain('Tasks.Edit');
    expect(element.querySelector('a[href="/administration/roles"]')).not.toBeNull();
  });

  it('lists the changes that wait for the next start, each with a way to cancel', async () => {
    const { element } = await create([plugin()], [{ id: 'Acme.Tasks', name: 'Tasks', version: '2.0.0', action: 'Upgrade', stagedAt: '2026-09-25T00:00:00' }]);

    expect(element.querySelector('.plugins-list__pending')?.textContent).toContain('Upgrade: Tasks 2.0.0');
    expect(element.querySelector('.plugins-list__pending button')).not.toBeNull();
  });

  it('offers rollback only when an earlier version exists', async () => {
    const without = await create([plugin({ canRollback: false })]);
    expect(without.element.textContent).not.toContain('Roll back');

    TestBed.resetTestingModule();
    TestBed.configureTestingModule({
      providers: [provideRouter([]), { provide: PluginsApiService, useValue: api }, { provide: PluginsService, useValue: pluginsService }, { provide: PermissionService, useValue: { has: () => true } }]
    });
    const withPrevious = await create([plugin({ canRollback: true })]);
    expect(withPrevious.element.textContent).toContain('Roll back');
  });

  it('uninstalls after confirmation and refreshes the menu', async () => {
    api.uninstall.mockReturnValue(of(undefined));
    const { page } = await create([plugin()]);

    await page.uninstall(plugin());

    expect(api.uninstall).toHaveBeenCalledWith('Acme.Tasks');
    expect(pluginsService.loadAsync).toHaveBeenCalled();
  });

  it('does nothing when the administrator declines the confirmation', async () => {
    (window.confirm as ReturnType<typeof vi.fn>).mockReturnValue(false);
    const { page } = await create([plugin({ canRollback: true })]);

    await page.uninstall(plugin());
    await page.rollback(plugin());

    expect(api.uninstall).not.toHaveBeenCalled();
    expect(api.rollback).not.toHaveBeenCalled();
  });

  it('hides install, uninstall and rollback from someone who cannot manage plugins', async () => {
    canManage = false;
    const { element } = await create([plugin({ canRollback: true })]);

    expect(element.textContent).not.toContain('Install plugin');
    expect(element.textContent).not.toContain('Uninstall');
    expect(element.textContent).not.toContain('Roll back');
  });

  it('marks a plugin that is waiting to be uninstalled and stops offering actions on it', async () => {
    const { element } = await create([plugin({ pendingUninstall: true })]);

    expect(element.textContent).toContain('Uninstalling at next start');
    expect(element.querySelector('.plugins-list__actions')).toBeNull();
  });

  it('shows the state of each plugin and the compatibility checks, opening them when one failed', async () => {
    const { element } = await create([
      plugin({ state: 'Failed', diagnostics: [{ check: 'Dependency', ok: false, detail: 'Acme.Base (1.0.0 to any) is not installed.' }] })
    ]);

    expect(element.textContent).toContain('Failed');
    expect(element.textContent).toContain('Acme.Base (1.0.0 to any) is not installed.');
    expect(element.querySelector('.plugins-list__check--bad')).not.toBeNull();
    expect((element.querySelector('details') as HTMLDetailsElement).open).toBe(true);
  });

  it('keeps the checks closed when everything passes', async () => {
    const { element } = await create([plugin()]);

    expect(element.textContent).toContain('Compatibility checks');
    expect(element.querySelector('.plugins-list__check--bad')).toBeNull();
    expect((element.querySelector('details') as HTMLDetailsElement).open).toBe(false);
  });

  it('offers a settings shortcut only for a plugin that defines settings', async () => {
    const { element } = await create([plugin({ settingsGroup: 'Tasks' }), plugin({ id: 'Acme.Other', name: 'Other', settingsGroup: null })]);

    expect(element.querySelectorAll('a[href="/administration/settings"]').length).toBe(1);
  });

  it('cancels a pending change and reloads', async () => {
    api.cancelPending.mockReturnValue(of(undefined));
    const change: PendingPluginChangeDto = { id: 'Acme.Tasks', name: 'Tasks', version: '2.0.0', action: 'Upgrade', stagedAt: '2026-09-25T00:00:00' };
    const { page } = await create([plugin()], [change]);

    await page.cancel(change);

    expect(api.cancelPending).toHaveBeenCalledWith('Acme.Tasks');
    expect(api.list).toHaveBeenCalledTimes(2);
  });

  it('shows the platform version range a plugin declares', async () => {
    const { page } = await create([plugin({ minHostVersion: '1.0.0', maxHostVersion: '2.0.0' })]);

    expect(page.hostRange(plugin({ minHostVersion: '1.0.0', maxHostVersion: '2.0.0' }))).toBe('1.0.0 – 2.0.0');
    expect(page.hostRange(plugin())).toBeNull();
  });
});
