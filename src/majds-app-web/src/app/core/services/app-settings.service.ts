import { HttpClient } from '@angular/common/http';
import { Injectable, inject, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { environment } from '../../../environments/environment';
import { ResponseDto } from '../models/response-dto';

/**
 * Loads the client-visible settings (F-Settings, SettingDefinition.IsVisibleToClient) after login —
 * same load-once-per-session shape as PermissionService — so parts of the shell every signed-in user
 * sees (e.g. the application name) reflect an admin's configured value instead of a hard-coded string.
 */
@Injectable({ providedIn: 'root' })
export class AppSettingsService {
  private readonly http = inject(HttpClient);
  private readonly settings = signal<Readonly<Record<string, string>>>({});
  private loaded = false;

  async loadAsync(): Promise<void> {
    try {
      const response = await firstValueFrom(
        this.http.get<ResponseDto<Record<string, string>>>(`${environment.apiBaseUrl}/settings/public`)
      );
      this.settings.set(response.data ?? {});
    } finally {
      this.loaded = true;
    }
  }

  clear(): void {
    this.settings.set({});
    this.loaded = false;
  }

  isLoaded(): boolean {
    return this.loaded;
  }

  get(name: string, fallback = ''): string {
    return this.settings()[name] ?? fallback;
  }
}
