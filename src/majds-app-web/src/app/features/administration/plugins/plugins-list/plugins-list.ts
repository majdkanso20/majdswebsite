import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { MatCardModule } from '@angular/material/card';
import { MatChipsModule } from '@angular/material/chips';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { MatSnackBar } from '@angular/material/snack-bar';
import { TranslatePipe } from '../../../../core/i18n/translate.pipe';
import { PermissionService } from '../../../../core/services/permission.service';
import { PluginsService } from '../../../../core/services/plugins.service';
import { InstalledPluginDto, PluginsApiService } from '../plugins-api.service';

/** Admin screen for P5's plugin registry (FR-PLUG-040): lists what's discovered from the plugins/
 *  folder and lets an administrator enable/disable each one live, no restart needed. */
@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'app-plugins-list',
  imports: [TranslatePipe, MatCardModule, MatChipsModule, MatSlideToggleModule],
  styleUrl: './plugins-list.scss',
  templateUrl: './plugins-list.html'
})
export class PluginsList {
  private readonly api = inject(PluginsApiService);
  private readonly snackBar = inject(MatSnackBar);
  private readonly plugins$ = inject(PluginsService);
  readonly canManage = inject(PermissionService).has('Plugins.Manage');

  readonly plugins = signal<InstalledPluginDto[]>([]);

  constructor() {
    void this.load();
  }

  async toggle(plugin: InstalledPluginDto, enabled: boolean): Promise<void> {
    try {
      await firstValueFrom(this.api.setEnabled(plugin.id, enabled));
      await this.plugins$.loadAsync(); // menu and route access follow immediately, no reload
      this.snackBar.open(`${plugin.name}: ${enabled ? 'enabled' : 'disabled'}.`, 'Dismiss', { duration: 3000 });
    } catch {
      this.snackBar.open('Something went wrong.', 'Dismiss', { duration: 4000 });
    }
    await this.load();
  }

  private async load(): Promise<void> {
    this.plugins.set(await firstValueFrom(this.api.list()));
  }
}
