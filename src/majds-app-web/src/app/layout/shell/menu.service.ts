import { Injectable, computed, inject, signal } from '@angular/core';
import { FeaturesService } from '../../core/services/features.service';
import { PermissionService } from '../../core/services/permission.service';
import { MenuItem } from './menu.model';

/**
 * Data-driven navigation registry (U3 FR-SHELL-002). New features push their own entry here
 * instead of the shell hard-coding routes. Items are filtered by `permission` against the same
 * PermissionService the route guards use, so nav and guards can't drift (Agent Notes, U3).
 * Contributions made with a `source` (plugins) can be replaced or removed as a unit (FR-SHELL-008).
 */
@Injectable({ providedIn: 'root' })
export class MenuService {
  private readonly permissionService = inject(PermissionService);
  private readonly featuresService = inject(FeaturesService);

  private readonly items = signal<MenuItem[]>([
    { label: 'Dashboard', icon: 'dashboard', route: '/dashboard' },
    { label: 'Files', icon: 'folder', route: '/files', feature: 'Files' },
    { label: 'My exports', icon: 'cloud_download', route: '/exports', feature: 'Files' },
    { label: 'Import history', icon: 'upload_file', route: '/imports', feature: 'Imports' },
    {
      label: 'Administration',
      icon: 'admin_panel_settings',
      children: [
        { label: 'Users', icon: 'group', route: '/administration/users', permission: 'Users.View' },
        { label: 'Roles', icon: 'shield_person', route: '/administration/roles', permission: 'Roles.View' },
        { label: 'Settings', icon: 'settings', route: '/administration/settings', permission: 'Settings.View' },
        { label: 'Background Jobs', icon: 'schedule', route: '/administration/jobs', permission: 'Jobs.View' },
        { label: 'Features', icon: 'toggle_on', route: '/administration/features', permission: 'Features.View' },
        { label: 'Audit Log', icon: 'history', route: '/administration/audit-log', permission: 'Audit.View' },
        { label: 'Plugins', icon: 'extension', route: '/administration/plugins', permission: 'Plugins.View' }
      ]
    }
  ]);

  readonly visibleItems = computed(() => this.filter(this.items()));

  register(item: MenuItem): void {
    this.items.update((current) => [...current, item]);
  }

  /** Replaces everything previously contributed by `source`, so a disabled plugin's entries disappear without a reload. */
  replaceSource(source: string, contributed: MenuItem[]): void {
    this.items.update((current) => [
      ...current.filter((item) => item.source !== source),
      ...contributed.map((item) => ({ ...item, source }))
    ]);
  }

  private filter(items: MenuItem[]): MenuItem[] {
    return items
      .map((item) => (item.children ? { ...item, children: this.filter(item.children) } : item))
      .filter((item) =>
        item.children
          ? item.children.length > 0
          : (!item.permission || this.permissionService.has(item.permission)) &&
            (!item.feature || this.featuresService.isEnabled(item.feature))
      );
  }
}
