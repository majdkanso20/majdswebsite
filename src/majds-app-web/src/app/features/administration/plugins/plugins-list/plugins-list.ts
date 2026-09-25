import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatChipsModule } from '@angular/material/chips';
import { MatDialog } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { MatSnackBar } from '@angular/material/snack-bar';
import { TranslatePipe } from '../../../../core/i18n/translate.pipe';
import { PermissionService } from '../../../../core/services/permission.service';
import { PluginsService } from '../../../../core/services/plugins.service';
import { PluginInstallDialog } from '../plugin-install-dialog/plugin-install-dialog';
import { InstalledPluginDto, PendingPluginChangeDto, PluginsApiService } from '../plugins-api.service';

/**
 * Admin screen for P5's plugin registry (FR-PLUG-040): what is installed, its declared permissions and menu entries,
 * enable/disable (live), install or upgrade from a package, roll back, and uninstall. Installs, upgrades, rollbacks and
 * uninstalls are verified now and applied the next time the API starts, so they are listed as pending until then.
 */
@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'app-plugins-list',
  imports: [TranslatePipe, RouterLink, MatButtonModule, MatCardModule, MatChipsModule, MatIconModule, MatSlideToggleModule],
  styleUrl: './plugins-list.scss',
  templateUrl: './plugins-list.html'
})
export class PluginsList {
  private readonly api = inject(PluginsApiService);
  private readonly snackBar = inject(MatSnackBar);
  private readonly dialog = inject(MatDialog);
  private readonly plugins$ = inject(PluginsService);
  readonly canManage = inject(PermissionService).has('Plugins.Manage');

  readonly plugins = signal<InstalledPluginDto[]>([]);
  readonly pending = signal<PendingPluginChangeDto[]>([]);

  constructor() {
    void this.load();
  }

  async toggle(plugin: InstalledPluginDto, enabled: boolean): Promise<void> {
    await this.run(async () => {
      await firstValueFrom(this.api.setEnabled(plugin.id, enabled));
      await this.plugins$.loadAsync(); // menu and route access follow immediately, no reload
      this.snackBar.open(`${plugin.name}: ${enabled ? 'enabled' : 'disabled'}.`, 'Dismiss', { duration: 3000 });
    });
  }

  openInstall(): void {
    this.dialog
      .open<PluginInstallDialog, undefined, boolean>(PluginInstallDialog, { width: 'min(640px, 95vw)', maxWidth: '95vw' })
      .afterClosed()
      .subscribe((staged) => {
        if (staged) void this.load();
      });
  }

  async uninstall(plugin: InstalledPluginDto): Promise<void> {
    if (!confirm(`Uninstall ${plugin.name}? It stops working now, its permissions are removed from every role and user, and its files are deleted the next time the API starts. Its data is kept.`)) return;

    await this.run(async () => {
      await firstValueFrom(this.api.uninstall(plugin.id));
      await this.plugins$.loadAsync();
      this.snackBar.open(`${plugin.name} was uninstalled. Its files are removed at the next start.`, 'Dismiss', { duration: 5000 });
    });
  }

  async rollback(plugin: InstalledPluginDto): Promise<void> {
    if (!confirm(`Restore the previous version of ${plugin.name}? It takes effect the next time the API starts.`)) return;

    await this.run(async () => {
      const staged = await firstValueFrom(this.api.rollback(plugin.id));
      this.snackBar.open(`Version ${staged.version} of ${plugin.name} will be restored at the next start.`, 'Dismiss', { duration: 5000 });
    });
  }

  async cancel(change: PendingPluginChangeDto): Promise<void> {
    await this.run(async () => {
      await firstValueFrom(this.api.cancelPending(change.id));
      await this.plugins$.loadAsync();
      this.snackBar.open('The pending change was cancelled.', 'Dismiss', { duration: 3000 });
    });
  }

  /** True when the plugin declares a platform version range, shown so an incompatibility is visible before it bites. */
  hostRange(plugin: InstalledPluginDto): string | null {
    if (!plugin.minHostVersion && !plugin.maxHostVersion) return null;
    return `${plugin.minHostVersion ?? '…'} – ${plugin.maxHostVersion ?? '…'}`;
  }

  private async run(action: () => Promise<void>): Promise<void> {
    try {
      await action();
    } catch (err) {
      const message = (err as { error?: { errors?: string[]; message?: string } })?.error;
      this.snackBar.open(message?.errors?.[0] ?? message?.message ?? 'Something went wrong.', 'Dismiss', { duration: 5000 });
    }
    await this.load();
  }

  private async load(): Promise<void> {
    const [plugins, pending] = await Promise.all([firstValueFrom(this.api.list()), firstValueFrom(this.api.pending())]);
    this.plugins.set(plugins);
    this.pending.set(pending);
  }
}
