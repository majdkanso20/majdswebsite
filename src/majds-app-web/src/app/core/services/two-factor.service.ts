import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { environment } from '../../../environments/environment';

export interface TwoFactorState {
  sharedKey: string;
  recoveryCodesLeft: number;
  recoveryCodes?: string[] | null;
  isTwoFactorEnabled: boolean;
  isMachineRemembered: boolean;
}

/**
 * Two-factor management (F-Account) over ASP.NET Core Identity's built-in `/manage/2fa` endpoint —
 * the same authenticator key and recovery codes the original Razor Pages site uses, so an account set
 * up there keeps working here and vice versa. It is not wrapped in ResponseDto: it's Identity's own API.
 */
@Injectable({ providedIn: 'root' })
export class TwoFactorService {
  private readonly http = inject(HttpClient);
  private readonly url = `${environment.apiBaseUrl}/identity/manage/2fa`;

  /** Current state; also returns the authenticator key to show/QR when setting up. */
  status(): Promise<TwoFactorState> {
    return firstValueFrom(this.http.post<TwoFactorState>(this.url, {}));
  }

  /** Verifies the first code from the authenticator app; on success returns the one-time recovery codes. */
  enable(twoFactorCode: string): Promise<TwoFactorState> {
    return firstValueFrom(this.http.post<TwoFactorState>(this.url, { enable: true, twoFactorCode }));
  }

  disable(): Promise<TwoFactorState> {
    return firstValueFrom(this.http.post<TwoFactorState>(this.url, { enable: false }));
  }

  newRecoveryCodes(): Promise<TwoFactorState> {
    return firstValueFrom(this.http.post<TwoFactorState>(this.url, { resetRecoveryCodes: true }));
  }
}
