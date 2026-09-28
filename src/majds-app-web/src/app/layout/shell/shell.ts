import { TranslatePipe } from '../../core/i18n/translate.pipe';
import { ChangeDetectionStrategy, Component, DestroyRef, computed, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { BreakpointObserver, Breakpoints } from '@angular/cdk/layout';
import { MatSidenavModule } from '@angular/material/sidenav';
import { MatToolbarModule } from '@angular/material/toolbar';
import { MatListModule } from '@angular/material/list';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { MatMenuModule } from '@angular/material/menu';
import { MatDividerModule } from '@angular/material/divider';
import { Router } from '@angular/router';
import { map } from 'rxjs';
import { MenuService } from './menu.service';
import { ThemeService, ThemeMode, SKINS, SkinId } from '../../theme/theme.service';
import { AuthService } from '../../core/services/auth.service';
import { PermissionService } from '../../core/services/permission.service';
import { LANGUAGES, LocalizationService } from '../../core/i18n/localization.service';
import { GlobalSearch } from '../../shared/components/global-search/global-search';
import { FeaturesService } from '../../core/services/features.service';
import { AccountService } from '../../core/services/account.service';
import { UserSettingsService } from '../../core/services/user-settings.service';
import { NotificationsService } from '../../core/services/notifications.service';
import { MatBadgeModule } from '@angular/material/badge';
import { channelsOutsideProd } from '../../core/notifications/delivery-environment';
import { AppSettingsService } from '../../core/services/app-settings.service';

/**
 * The mobile-first, responsive app shell every feature inherits (U3): a Material sidenav that's
 * persistent on desktop and an overlay (hamburger-triggered) on mobile (FR-SHELL-001, AC-SHELL-3),
 * a header with the theme toggle and (future) user menu / notification bell, and permission-driven
 * navigation from MenuService (FR-SHELL-002).
 */
@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'app-shell',
  imports: [TranslatePipe, GlobalSearch, MatBadgeModule,
    RouterOutlet,
    RouterLink,
    RouterLinkActive,
    MatSidenavModule,
    MatToolbarModule,
    MatListModule,
    MatIconModule,
    MatButtonModule,
    MatMenuModule,
    MatDividerModule
  ],
  styleUrl: './shell.scss',
  templateUrl: './shell.html'
})
export class Shell {
  private readonly breakpointObserver = inject(BreakpointObserver);
  private readonly menuService = inject(MenuService);
  private readonly themeService = inject(ThemeService);
  private readonly authService = inject(AuthService);
  private readonly permissionService = inject(PermissionService);
  /** The user menu offers Settings only to someone who may open it (FR-SHELL-003). */
  protected readonly canOpenSettings = () => this.permissionService.has('Settings.View');
  private readonly appSettingsService = inject(AppSettingsService);
  private readonly router = inject(Router);

  /** Opens the page a notification points to (for example a finished export). */
  openNotification(link: string): void {
    void this.router.navigateByUrl(link);
  }

  private readonly localization = inject(LocalizationService);
  private readonly userSettings = inject(UserSettingsService);

  readonly languages = LANGUAGES;
  readonly notifications = inject(NotificationsService);
  readonly account = inject(AccountService);
  readonly features = inject(FeaturesService);

  constructor() {
    void this.account.loadAsync();
    const notificationsOn = this.features.isEnabled('Notifications');
    if (notificationsOn) void this.notifications.connectAsync();
    inject(DestroyRef).onDestroy(() => void this.notifications.disconnectAsync());
  }

  readonly menuItems = this.menuService.visibleItems;
  readonly themeMode = this.themeService.mode;
  readonly skins = SKINS;
  readonly skin = this.themeService.skin;
  readonly currentUser = this.authService.currentUser;
  /** One line for each channel that is not in Prod (Dev: nothing is sent; Test: only to the test recipient), or none. */
  readonly deliveryNotices = computed(() =>
    channelsOutsideProd((name, fallback) => this.appSettingsService.get(name, fallback)).map((c) =>
      this.localization.translate(c.mode === 'Dev' ? 'Dev mode: {0} notifications are not sent, they are only logged.' : 'Test mode: {0} notifications go only to the test recipient.', this.localization.translate(c.channel))
    )
  );
  readonly applicationName = computed(() => this.appSettingsService.get('General.ApplicationName', "Majd's App"));

  /** Mobile = overlay sidenav that closes on navigation; desktop = persistent, always open. */
  readonly isMobile = toSignal(
    this.breakpointObserver.observe(Breakpoints.Handset).pipe(map((result) => result.matches)),
    { initialValue: false }
  );

  readonly sidenavMode = computed(() => (this.isMobile() ? 'over' : 'side'));

  setTheme(mode: ThemeMode): void {
    this.themeService.setMode(mode);
  }

  setSkin(skin: SkinId): void {
    this.themeService.setSkin(skin);
  }

  /** Skip link target (FR-SHELL-007): moves keyboard focus straight to the page content. */
  focusContent(content: HTMLElement): void {
    content.focus();
  }

  setLanguage(code: string): void {
    void this.localization.set(code);
    void this.userSettings.saveAsync([{ name: 'General.DefaultLanguage', value: code }]).catch(() => undefined);
  }

  signOut(): void {
    this.authService.logout();
    this.permissionService.clear();
    this.appSettingsService.clear();
    this.notifications.clear();
    this.account.clear();
    this.features.clear();
    this.router.navigate(['/login']);
  }
}
