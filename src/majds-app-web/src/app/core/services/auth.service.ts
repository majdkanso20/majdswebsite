import { HttpClient } from '@angular/common/http';
import { Injectable, inject, signal } from '@angular/core';
import { Observable, firstValueFrom, tap } from 'rxjs';
import { environment } from '../../../environments/environment';
import { CurrentUser, LoginRequest, LoginResponse } from '../models/auth.models';

const ACCESS_TOKEN_KEY = 'majds-app.accessToken';
const USER_EMAIL_KEY = 'majds-app.userEmail';

/**
 * Bridges to the existing Identity system's bearer-token endpoints (AS-4) — not a new auth system,
 * just a thin client for it. Token storage is localStorage for now; a refresh-token rotation flow
 * can be layered in later without changing this service's public surface.
 */
@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiBaseUrl}/identity`;

  readonly currentUser = signal<CurrentUser | null>(this.readStoredUser());

  get accessToken(): string | null {
    try {
      return localStorage.getItem(ACCESS_TOKEN_KEY);
    } catch {
      return null;
    }
  }

  isAuthenticated(): boolean {
    return this.accessToken !== null;
  }

  login(request: LoginRequest): Observable<LoginResponse> {
    return this.http.post<LoginResponse>(`${this.baseUrl}/login`, request).pipe(
      tap((response) => {
        this.storeSession(response.accessToken, request.email);
      })
    );
  }

  /** Providers (e.g. "google") the API has credentials for; empty if none or the API is unreachable. */
  async externalProviders(): Promise<string[]> {
    try {
      const response = await firstValueFrom(
        this.http.get<{ data: string[] | null }>(`${environment.apiBaseUrl}/external/providers`)
      );
      return response.data ?? [];
    } catch {
      return [];
    }
  }

  /** Whether the self-registration setting is on (drives the "Create account" link). */
  async registrationOpen(): Promise<boolean> {
    try {
      const response = await firstValueFrom(
        this.http.get<{ data: boolean | null }>(`${environment.apiBaseUrl}/account/registration-open`)
      );
      return response.data === true;
    } catch {
      return false;
    }
  }

  /** Full-page redirect: the OAuth handshake can't run inside an XHR. The API sends the browser back
   *  to /login/callback with the result in the URL fragment. */
  startExternalLogin(provider: string): void {
    const returnUrl = encodeURIComponent(`${window.location.origin}/login/callback`);
    window.location.href = `${environment.apiBaseUrl}/external/${provider}/start?returnUrl=${returnUrl}`;
  }

  completeExternalLogin(accessToken: string, email: string): void {
    this.storeSession(accessToken, email);
  }

  logout(): void {
    try {
      localStorage.removeItem(ACCESS_TOKEN_KEY);
      localStorage.removeItem(USER_EMAIL_KEY);
    } catch {
      // Ignore storage errors — clearing the in-memory signal below still logs the user out of the UI.
    }
    this.currentUser.set(null);
  }

  private storeSession(accessToken: string, email: string): void {
    try {
      localStorage.setItem(ACCESS_TOKEN_KEY, accessToken);
      localStorage.setItem(USER_EMAIL_KEY, email);
    } catch {
      // Private browsing / storage disabled: the session just won't survive a reload.
    }
    this.currentUser.set({ email });
  }

  private readStoredUser(): CurrentUser | null {
    try {
      const email = localStorage.getItem(USER_EMAIL_KEY);
      const token = localStorage.getItem(ACCESS_TOKEN_KEY);
      return email && token ? { email } : null;
    } catch {
      return null;
    }
  }
}
