import { HttpClient } from '@angular/common/http';
import { Injectable, inject, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { environment } from '../../../environments/environment';
import { ResponseDto } from '../models/response-dto';

/**
 * Loads the current user's effective permissions after login (F-Authorization FR-AUTHZ-005) and
 * exposes a synchronous check the `hasPermission` directive and route guards use — one source of
 * truth so nav visibility and route access can't drift (U3 Agent Notes).
 */
@Injectable({ providedIn: 'root' })
export class PermissionService {
  private readonly http = inject(HttpClient);
  private readonly permissions = signal<ReadonlySet<string>>(new Set());
  private loaded = false;

  async loadAsync(): Promise<void> {
    try {
      const response = await firstValueFrom(
        this.http.get<ResponseDto<string[]>>(`${environment.apiBaseUrl}/session/permissions`)
      );
      this.permissions.set(new Set(response.data ?? []));
    } finally {
      this.loaded = true;
    }
  }

  clear(): void {
    this.permissions.set(new Set());
    this.loaded = false;
  }

  isLoaded(): boolean {
    return this.loaded;
  }

  has(permission: string): boolean {
    return this.permissions().has(permission);
  }
}
