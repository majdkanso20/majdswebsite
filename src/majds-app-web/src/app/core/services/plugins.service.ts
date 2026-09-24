import { HttpClient } from '@angular/common/http';
import { Injectable, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { environment } from '../../../environments/environment';
import { permissionGuard } from '../guards/permission.guard';
import { ResponseDto } from '../models/response-dto';
import { MenuService } from '../../layout/shell/menu.service';

interface PluginMenuEntryDto {
  pluginId: string;
  label: string;
  icon: string;
  route: string;
  permission: string | null;
  order: number;
}

/**
 * P5's frontend half: fetches the enabled plugins' menu contributions (FR-PLUG-020/FR-SHELL-008) and
 * — since this project's plugin UI story is the "metadata renderer" option rather than Native
 * Federation (see PluginCrudPage) — registers one generic route per plugin at runtime, all pointing
 * at the same host-compiled page. Called once from authGuard and the app initializer, and again
 * whenever an administrator enables or disables a plugin, so the menu follows without a reload.
 */
@Injectable({ providedIn: 'root' })
export class PluginsService {
  private readonly http = inject(HttpClient);
  private readonly router = inject(Router);
  private readonly menuService = inject(MenuService);

  private readonly loaded = signal(false);
  private readonly enabledRoutes = signal<ReadonlySet<string>>(new Set());

  isLoaded(): boolean {
    return this.loaded();
  }

  /** Whether the plugin owning this route is currently enabled (the route guard uses it). */
  isRouteEnabled(route: string): boolean {
    return this.enabledRoutes().has(route);
  }

  async loadAsync(): Promise<void> {
    let list: PluginMenuEntryDto[] = [];
    try {
      const response = await firstValueFrom(
        this.http.get<ResponseDto<PluginMenuEntryDto[]>>(`${environment.apiBaseUrl}/plugins/manifest`)
      );
      list = response.data ?? [];
    } catch {
      // Plugins are optional — a fetch failure shouldn't block the rest of the app from loading.
    }

    this.enabledRoutes.set(new Set(list.map((entry) => entry.route)));
    this.menuService.replaceSource(
      'plugins',
      list.map((entry) => ({
        label: entry.label,
        icon: entry.icon,
        route: `/${entry.route}`,
        permission: entry.permission ?? undefined
      }))
    );

    const shellRoute = this.router.config.find((r) => r.path === '');
    if (shellRoute?.children) {
      for (const entry of list) {
        if (shellRoute.children.some((c) => c.path === entry.route)) continue;
        shellRoute.children.push({
          path: entry.route,
          canActivate: [
            () => this.isRouteEnabled(entry.route) || this.router.createUrlTree(['/dashboard']),
            ...(entry.permission ? [permissionGuard(entry.permission)] : [])
          ],
          loadComponent: () =>
            import('../../features/plugins/plugin-crud-page/plugin-crud-page').then((m) => m.PluginCrudPage),
          data: { pluginId: entry.pluginId }
        });
      }
      this.router.resetConfig(this.router.config);
    }

    this.loaded.set(true);
  }
}
