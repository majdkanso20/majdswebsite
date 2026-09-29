import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatSnackBar } from '@angular/material/snack-bar';
import { TranslatePipe } from '../../../core/i18n/translate.pipe';
import { PushNotificationsService } from '../../../core/services/push-notifications.service';

/** Turn push notifications on or off for this browser (F-Notifications FR-NOTIF-002/009). A per-device setting,
 * not an account one — the same account signed in on another device subscribes (or not) separately. */
@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'app-push-notifications-card',
  imports: [TranslatePipe, MatButtonModule, MatCardModule],
  styleUrl: './push-notifications-card.scss',
  templateUrl: './push-notifications-card.html'
})
export class PushNotificationsCard {
  private readonly push = inject(PushNotificationsService);
  private readonly snackBar = inject(MatSnackBar);

  readonly supported = this.push.supported;
  readonly subscribed = signal(false);
  readonly busy = signal(false);

  constructor() {
    void this.refresh();
  }

  async enable(): Promise<void> {
    this.busy.set(true);
    try {
      await this.push.subscribeAsync();
      this.subscribed.set(true);
      this.snackBar.open('Push notifications are on for this device.', 'Dismiss', { duration: 3000 });
    } catch {
      this.snackBar.open('Could not turn on push notifications. Your browser may have blocked the permission.', 'Dismiss', { duration: 5000 });
    } finally {
      this.busy.set(false);
    }
  }

  async disable(): Promise<void> {
    this.busy.set(true);
    try {
      await this.push.unsubscribeAsync();
      this.subscribed.set(false);
      this.snackBar.open('Push notifications are off for this device.', 'Dismiss', { duration: 3000 });
    } catch {
      this.snackBar.open('Something went wrong.', 'Dismiss', { duration: 5000 });
    } finally {
      this.busy.set(false);
    }
  }

  private async refresh(): Promise<void> {
    if (this.supported) this.subscribed.set(await this.push.isSubscribedAsync());
  }
}
