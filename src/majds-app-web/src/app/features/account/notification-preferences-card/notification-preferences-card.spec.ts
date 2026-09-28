import { TestBed } from '@angular/core/testing';
import { MatSnackBar } from '@angular/material/snack-bar';
import { NotificationSubscription, NotificationsService } from '../../../core/services/notifications.service';
import { NotificationPreferencesCard } from './notification-preferences-card';

const rows = (): NotificationSubscription[] => [
  { type: 'General', inApp: true, email: true, channels: [{ name: 'Sms', displayName: 'SMS', enabled: false }] },
  { type: 'Security', inApp: true, email: true, channels: [{ name: 'Sms', displayName: 'SMS', enabled: true }] }
];

describe('NotificationPreferencesCard', () => {
  const service = { getSubscriptionsAsync: vi.fn(), saveSubscriptionsAsync: vi.fn() };

  async function create(data: NotificationSubscription[]) {
    service.getSubscriptionsAsync.mockResolvedValue(data);
    service.saveSubscriptionsAsync.mockResolvedValue(undefined);
    TestBed.configureTestingModule({
      providers: [{ provide: NotificationsService, useValue: service }, { provide: MatSnackBar, useValue: { open: vi.fn() } }]
    });
    const fixture = TestBed.createComponent(NotificationPreferencesCard);
    await fixture.whenStable();
    fixture.detectChanges();
    return { fixture, card: fixture.componentInstance, element: fixture.nativeElement as HTMLElement };
  }

  beforeEach(() => {
    service.getSubscriptionsAsync.mockReset();
    service.saveSubscriptionsAsync.mockReset();
  });

  it('shows the two built-in columns and one more for each channel a module added', async () => {
    const { element } = await create(rows());

    expect([...element.querySelectorAll('[role=columnheader]')].map((h) => h.textContent?.trim())).toEqual(['Type', 'In-app', 'Email', 'SMS']);
    expect(element.querySelectorAll('.prefs__row:not(.prefs__row--head) mat-checkbox').length).toBe(6);
  });

  it('shows only the built-in columns when no module has added a channel', async () => {
    const { element } = await create([{ type: 'General', inApp: true, email: true }]);

    expect([...element.querySelectorAll('[role=columnheader]')].map((h) => h.textContent?.trim())).toEqual(['Type', 'In-app', 'Email']);
  });

  it('remembers the choice made on an extra channel and sends it when saved', async () => {
    const { card } = await create(rows());

    card.toggleExtra('General', 'Sms', true);
    await card.save();

    const saved = service.saveSubscriptionsAsync.mock.calls[0][0] as NotificationSubscription[];
    expect(saved.find((r) => r.type === 'General')!.channels![0].enabled).toBe(true);
    expect(saved.find((r) => r.type === 'Security')!.channels![0].enabled).toBe(true);
    expect(card.isOn(saved[0], 'Sms')).toBe(true);
  });
});
