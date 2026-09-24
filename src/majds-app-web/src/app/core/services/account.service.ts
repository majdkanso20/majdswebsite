import { HttpClient } from '@angular/common/http';
import { Injectable, inject, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { environment } from '../../../environments/environment';
import { ResponseDto } from '../models/response-dto';

export interface Profile {
  id: string;
  email: string;
  fullName: string | null;
  phoneNumber: string | null;
  hasPicture: boolean;
  twoFactorEnabled: boolean;
  roles: string[];
}

/** The signed-in user's own account (F-Account): profile data plus the picture as a blob URL,
 *  fetched through HttpClient because the picture endpoint needs the bearer token. */
@Injectable({ providedIn: 'root' })
export class AccountService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiBaseUrl}/account`;

  readonly profile = signal<Profile | null>(null);
  readonly pictureUrl = signal<string | null>(null);

  async loadAsync(): Promise<void> {
    try {
      const response = await firstValueFrom(this.http.get<ResponseDto<Profile>>(`${this.baseUrl}/me`));
      this.profile.set(response.data);
      await this.loadPictureAsync(response.data?.hasPicture ?? false);
    } catch {
      // the shell still works without the avatar; the account page surfaces load errors itself
    }
  }

  async updateProfileAsync(fullName: string | null, phoneNumber: string | null): Promise<void> {
    await firstValueFrom(this.http.post(`${this.baseUrl}/update-profile`, { fullName, phoneNumber }));
    await this.loadAsync();
  }

  async changePasswordAsync(currentPassword: string, newPassword: string): Promise<void> {
    await firstValueFrom(this.http.post(`${this.baseUrl}/change-password`, { currentPassword, newPassword }));
  }

  async setPictureAsync(file: File): Promise<void> {
    const body = new FormData();
    body.append('file', file, file.name);
    await firstValueFrom(this.http.post(`${this.baseUrl}/picture`, body));
    await this.loadAsync();
  }

  clear(): void {
    this.profile.set(null);
    this.setPictureUrl(null);
  }

  private async loadPictureAsync(hasPicture: boolean): Promise<void> {
    if (!hasPicture) {
      this.setPictureUrl(null);
      return;
    }
    const blob = await firstValueFrom(this.http.get(`${this.baseUrl}/picture`, { responseType: 'blob' }));
    this.setPictureUrl(URL.createObjectURL(blob));
  }

  private setPictureUrl(url: string | null): void {
    const previous = this.pictureUrl();
    if (previous) URL.revokeObjectURL(previous);
    this.pictureUrl.set(url);
  }
}
