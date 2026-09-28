import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatSnackBar } from '@angular/material/snack-bar';
import { TranslatePipe } from '../../../core/i18n/translate.pipe';
import { ChannelChoice, NotificationSubscription, NotificationsService } from '../../../core/services/notifications.service';

/** Type × channel matrix (F-Account FR-ACCT-005 / F-Notifications FR-NOTIF-006): which kinds of
 *  notification reach the user in the app and by email. */
@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'app-notification-preferences-card',
  imports: [TranslatePipe, MatButtonModule, MatCardModule, MatCheckboxModule],
  styleUrl: './notification-preferences-card.scss',
  templateUrl: './notification-preferences-card.html'
})
export class NotificationPreferencesCard {
  private readonly notifications = inject(NotificationsService);
  private readonly snackBar = inject(MatSnackBar);

  readonly rows = signal<NotificationSubscription[]>([]);
  readonly saving = signal(false);

  constructor() {
    void this.notifications.getSubscriptionsAsync().then((rows) => this.rows.set(rows));
  }

  /** The extra channels (one column each), taken from the first row: every row lists the same ones. */
  readonly extraChannels = computed<ChannelChoice[]>(() => this.rows()[0]?.channels ?? []);
  readonly gridColumns = computed(() => `1fr 5rem 5rem${' 5rem'.repeat(this.extraChannels().length)}`);

  isOn(row: NotificationSubscription, channel: string): boolean {
    return row.channels?.find((c) => c.name === channel)?.enabled ?? false;
  }

  toggleExtra(type: string, channel: string, checked: boolean): void {
    this.rows.update((rows) =>
      rows.map((row) => (row.type === type ? { ...row, channels: row.channels?.map((c) => (c.name === channel ? { ...c, enabled: checked } : c)) } : row))
    );
  }

  toggle(type: string, channel: 'inApp' | 'email', checked: boolean): void {
    this.rows.update((rows) => rows.map((row) => (row.type === type ? { ...row, [channel]: checked } : row)));
  }

  async save(): Promise<void> {
    this.saving.set(true);
    try {
      await this.notifications.saveSubscriptionsAsync(this.rows());
      this.snackBar.open('Preferences saved.', 'Dismiss', { duration: 3000 });
    } catch {
      this.snackBar.open('Something went wrong.', 'Dismiss', { duration: 4000 });
    } finally {
      this.saving.set(false);
    }
  }
}
