import { HttpClient } from '@angular/common/http';
import { Injectable, inject, signal } from '@angular/core';
import { HttpTransportType, HubConnection, HubConnectionBuilder, HubConnectionState } from '@microsoft/signalr';
import { firstValueFrom } from 'rxjs';
import { MatSnackBar } from '@angular/material/snack-bar';
import { environment } from '../../../environments/environment';
import { PagedResponse, ResponseDto } from '../models/response-dto';
import { AuthService } from './auth.service';

export interface NotificationDto {
  id: number;
  type: string;
  title: string;
  message: string;
  isRead: boolean;
  createdAt: string;
  /** An in-app route the notification opens when clicked, or null. */
  link?: string | null;
}

export interface NotificationSubscription {
  type: string;
  inApp: boolean;
  email: boolean;
}

/**
 * The signed-in user's notifications (F-Notifications): unread badge plus the latest few, kept live
 * by a SignalR connection (FR-NOTIF-005). The list is re-fetched on every (re)connect so anything
 * that arrived while offline still shows up.
 */
@Injectable({ providedIn: 'root' })
export class NotificationsService {
  private readonly http = inject(HttpClient);
  private readonly auth = inject(AuthService);
  private readonly snackBar = inject(MatSnackBar);
  private readonly baseUrl = `${environment.apiBaseUrl}/notifications`;
  private connection: HubConnection | null = null;

  readonly unreadCount = signal(0);
  readonly recent = signal<NotificationDto[]>([]);

  async connectAsync(): Promise<void> {
    if (this.connection) return;

    // WebSockets with skipNegotiation: the token travels in the query string (browsers can't set
    // headers on a WebSocket) and it avoids the credentialed CORS negotiate request.
    const connection = new HubConnectionBuilder()
      .withUrl(environment.apiBaseUrl.replace(/\/api$/, '') + '/hubs/notifications', {
        accessTokenFactory: () => this.auth.accessToken ?? '',
        transport: HttpTransportType.WebSockets,
        skipNegotiation: true
      })
      .withAutomaticReconnect()
      .build();

    connection.on('notification', (n: NotificationDto) => {
      this.unreadCount.update((count) => count + 1);
      this.recent.update((list) => [n, ...list].slice(0, 8));
      this.snackBar.open(n.title, 'Dismiss', { duration: 4000 });
    });
    connection.onreconnected(() => void this.refreshAsync());

    this.connection = connection;
    try {
      await connection.start();
    } catch {
      // API unreachable: automatic reconnect only applies after a successful start, so leave a
      // clean slate and let the next connectAsync() (e.g. the next navigation) try again.
      this.connection = null;
    }
    await this.refreshAsync();
  }

  async disconnectAsync(): Promise<void> {
    const connection = this.connection;
    this.connection = null;
    if (connection && connection.state !== HubConnectionState.Disconnected) await connection.stop();
  }

  async refreshAsync(): Promise<void> {
    try {
      const [count, list] = await Promise.all([
        firstValueFrom(this.http.get<ResponseDto<number>>(`${this.baseUrl}/unread-count`)),
        firstValueFrom(
          this.http.get<ResponseDto<PagedResponse<NotificationDto>>>(`${this.baseUrl}/list`, {
            params: { page: 1, pageSize: 8 }
          })
        )
      ]);
      this.unreadCount.set(count.data ?? 0);
      this.recent.set(list.data?.items ?? []);
    } catch {
      // transient failure (e.g. API restarting) — the next reconnect refreshes again
    }
  }

  async markAllReadAsync(): Promise<void> {
    await firstValueFrom(this.http.post(`${this.baseUrl}/mark-read`, { ids: null }));
    await this.refreshAsync();
  }

  async getSubscriptionsAsync(): Promise<NotificationSubscription[]> {
    const response = await firstValueFrom(
      this.http.get<ResponseDto<NotificationSubscription[]>>(`${this.baseUrl}/subscriptions/get`)
    );
    return response.data ?? [];
  }

  async saveSubscriptionsAsync(items: NotificationSubscription[]): Promise<void> {
    await firstValueFrom(this.http.post(`${this.baseUrl}/subscriptions/update`, { items }));
  }

  clear(): void {
    void this.disconnectAsync();
    this.unreadCount.set(0);
    this.recent.set([]);
  }
}
