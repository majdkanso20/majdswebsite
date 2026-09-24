import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { environment } from '../../../environments/environment';
import { ResponseDto } from '../models/response-dto';
import { AppSettingsService } from './app-settings.service';

export interface MySetting {
  name: string;
  displayName: string;
  value: string;
  /** What the user gets with no personal override (the Application-scope value). */
  defaultValue: string;
}

/** The signed-in user's own setting overrides (F-Settings User scope: User > Application > default). */
@Injectable({ providedIn: 'root' })
export class UserSettingsService {
  private readonly http = inject(HttpClient);
  private readonly appSettings = inject(AppSettingsService);
  private readonly baseUrl = `${environment.apiBaseUrl}/settings`;

  async getMineAsync(): Promise<MySetting[]> {
    const response = await firstValueFrom(this.http.get<ResponseDto<MySetting[]>>(`${this.baseUrl}/my`));
    return response.data ?? [];
  }

  async saveAsync(items: { name: string; value: string }[]): Promise<void> {
    await firstValueFrom(this.http.post(`${this.baseUrl}/update-mine`, { items }));
    await this.appSettings.loadAsync(); // the shell reads the user-resolved values from here
  }
}
