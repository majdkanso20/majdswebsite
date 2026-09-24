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
}

@Injectable({ providedIn: 'root' })
export class PluginsApiService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiBaseUrl}/plugins`;

  list(): Observable<InstalledPluginDto[]> {
    return this.http.get<ResponseDto<InstalledPluginDto[]>>(`${this.baseUrl}/list`).pipe(map((r) => r.data ?? []));
  }

  setEnabled(pluginId: string, enabled: boolean): Observable<void> {
    return this.http
      .post<ResponseDto<null>>(`${this.baseUrl}/set-enabled`, { pluginId, enabled })
      .pipe(map(() => undefined));
  }
}
