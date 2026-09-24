import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, map } from 'rxjs';
import { environment } from '../../../../environments/environment';
import { ResponseDto } from '../../../core/models/response-dto';
import { SettingDto, SettingUpdateItem } from './settings.models';

@Injectable({ providedIn: 'root' })
export class SettingsApiService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiBaseUrl}/settings`;

  list(): Observable<SettingDto[]> {
    return this.http.get<ResponseDto<SettingDto[]>>(`${this.baseUrl}/list`).pipe(map((r) => r.data ?? []));
  }

  update(items: SettingUpdateItem[]): Observable<void> {
    return this.http.post<ResponseDto<null>>(`${this.baseUrl}/update`, { items }).pipe(map(() => undefined));
  }

  testEmail(): Observable<void> {
    return this.http
      .post<ResponseDto<null>>(`${environment.apiBaseUrl}/notifications/test-channel`, {})
      .pipe(map(() => undefined));
  }

  public(): Observable<Record<string, string>> {
    return this.http
      .get<ResponseDto<Record<string, string>>>(`${this.baseUrl}/public`)
      .pipe(map((r) => r.data ?? {}));
  }
}
