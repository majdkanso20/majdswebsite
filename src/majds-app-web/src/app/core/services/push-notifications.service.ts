import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { SwPush } from '@angular/service-worker';
import { firstValueFrom } from 'rxjs';
import { environment } from '../../../environments/environment';
import { ResponseDto } from '../models/response-dto';

/**
 * Push notifications for this browser (F-Notifications FR-NOTIF-002/009): a real channel, not a stub — an
 * administrator generates a VAPID key pair on the Notifications settings page and this service uses Angular's
 * built-in service worker (`SwPush`) to subscribe, which is also what shows the notification when one arrives
 * while the app is closed. No third-party push service account is involved on either side.
 */
@Injectable({ providedIn: 'root' })
export class PushNotificationsService {
  private readonly http = inject(HttpClient);
  private readonly swPush = inject(SwPush);
  private readonly baseUrl = `${environment.apiBaseUrl}/notifications/push`;

  /** False when this build has no service worker (for example `ng serve`) — push cannot work at all here. */
  get supported(): boolean {
    return this.swPush.isEnabled;
  }

  /** Whether this browser currently has an active subscription. */
  async isSubscribedAsync(): Promise<boolean> {
    if (!this.supported) return false;
    const subscription = await firstValueFrom(this.swPush.subscription);
    return subscription !== null;
  }

  /** Asks the browser to subscribe (prompts for the Notification permission if needed) and registers it with the server. */
  async subscribeAsync(): Promise<void> {
    const publicKey = await firstValueFrom(this.http.get<ResponseDto<string | null>>(`${this.baseUrl}/vapid-public-key`));
    if (!publicKey.data) throw new Error('Push notifications are not set up on this server yet.');

    const subscription = await this.swPush.requestSubscription({ serverPublicKey: publicKey.data });
    const json = subscription.toJSON();
    await firstValueFrom(
      this.http.post<ResponseDto<null>>(`${this.baseUrl}/subscribe`, {
        endpoint: json.endpoint,
        p256dh: json.keys?.['p256dh'],
        auth: json.keys?.['auth']
      })
    );
  }

  /** Unsubscribes this browser and tells the server to stop sending to it. */
  async unsubscribeAsync(): Promise<void> {
    const subscription = await firstValueFrom(this.swPush.subscription);
    if (!subscription) return;

    const endpoint = subscription.endpoint;
    await subscription.unsubscribe();
    await firstValueFrom(this.http.post<ResponseDto<null>>(`${this.baseUrl}/unsubscribe`, { endpoint }));
  }
}
