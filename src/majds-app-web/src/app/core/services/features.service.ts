import { HttpClient } from '@angular/common/http';
import { Injectable, inject, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { environment } from '../../../environments/environment';
import { ResponseDto } from '../models/response-dto';

/** Which feature flags (F-Features) are on. The backend enforces them; this only hides the UI for
 *  disabled modules. Loaded with permissions after sign-in, same lifecycle as PermissionService. */
@Injectable({ providedIn: 'root' })
export class FeaturesService {
  private readonly http = inject(HttpClient);
  private readonly enabled = signal<ReadonlySet<string>>(new Set());
  private loaded = false;

  async loadAsync(): Promise<void> {
    try {
      const response = await firstValueFrom(this.http.get<ResponseDto<string[]>>(`${environment.apiBaseUrl}/features/enabled`));
      this.enabled.set(new Set(response.data ?? []));
    } finally {
      this.loaded = true;
    }
  }

  isLoaded(): boolean {
    return this.loaded;
  }

  clear(): void {
    this.enabled.set(new Set());
    this.loaded = false;
  }

  isEnabled(name: string): boolean {
    return this.enabled().has(name);
  }
}
