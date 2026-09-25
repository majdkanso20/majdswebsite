import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { map, Observable } from 'rxjs';
import { environment } from '../../../../environments/environment';
import { ResponseDto } from '../../../core/models/response-dto';

export interface InstalledPluginDto {
  id: string;
  name: string;
  version: string;
  author: string;
  isEnabled: boolean;
  lastError: string | null;
  discoveredAt: string;
  /** Permissions the plugin declares (FR-PLUG-042). */
  permissions: string[];
  menuEntries: string[];
  /** An earlier version is kept and can be restored (FR-PLUG-038). */
  canRollback: boolean;
  pendingUninstall: boolean;
  minHostVersion: string | null;
  maxHostVersion: string | null;
}

/** A verified package staged to be installed, upgraded or rolled back at the next start. */
export interface PluginChangeDto {
  id: string;
  name: string;
  version: string;
  action: 'Install' | 'Upgrade' | 'Rollback';
  previousVersion: string | null;
  /** SHA-256 of the uploaded package, to compare with the publisher's checksum (FR-PLUG-036). */
  sha256: string;
  restartRequired: boolean;
}

export interface PendingPluginChangeDto {
  id: string;
  name: string;
  version: string;
  action: 'Install' | 'Upgrade' | 'Rollback' | 'Uninstall';
  stagedAt: string;
}

@Injectable({ providedIn: 'root' })
export class PluginsApiService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiBaseUrl}/plugins`;

  list(): Observable<InstalledPluginDto[]> {
    return this.http.get<ResponseDto<InstalledPluginDto[]>>(`${this.baseUrl}/list`).pipe(map((r) => r.data ?? []));
  }

  pending(): Observable<PendingPluginChangeDto[]> {
    return this.http.get<ResponseDto<PendingPluginChangeDto[]>>(`${this.baseUrl}/pending`).pipe(map((r) => r.data ?? []));
  }

  setEnabled(pluginId: string, enabled: boolean): Observable<void> {
    return this.http
      .post<ResponseDto<null>>(`${this.baseUrl}/set-enabled`, { pluginId, enabled })
      .pipe(map(() => undefined));
  }

  /** Uploads a plugin package (.zip). It is verified and staged; nothing running changes until the next start. */
  install(file: File, sha256?: string): Observable<PluginChangeDto> {
    const body = new FormData();
    body.append('file', file, file.name);
    if (sha256?.trim()) body.append('sha256', sha256.trim());
    return this.http.post<ResponseDto<PluginChangeDto>>(`${this.baseUrl}/install`, body).pipe(map((r) => r.data!));
  }

  uninstall(pluginId: string): Observable<void> {
    return this.http.post<ResponseDto<null>>(`${this.baseUrl}/uninstall`, { pluginId }).pipe(map(() => undefined));
  }

  rollback(pluginId: string): Observable<PluginChangeDto> {
    return this.http.post<ResponseDto<PluginChangeDto>>(`${this.baseUrl}/rollback`, { pluginId }).pipe(map((r) => r.data!));
  }

  cancelPending(pluginId: string): Observable<void> {
    return this.http.post<ResponseDto<null>>(`${this.baseUrl}/cancel-pending`, { pluginId }).pipe(map(() => undefined));
  }
}
