import { ChangeDetectionStrategy, Component, ElementRef, afterNextRender, inject, signal, viewChild } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { environment } from '../../../../environments/environment';
import { LocalizationService } from '../../../core/i18n/localization.service';
import { TranslatePipe } from '../../../core/i18n/translate.pipe';
import { PluginBundleLoader, PluginElementRoute, compatibilityProblem } from '../../../core/plugins/plugin-ui';
import { AuthService } from '../../../core/services/auth.service';
import { ErrorState } from '../../../shared/components/error-state/error-state';

/**
 * Shows a screen a plugin ships as a pre-built custom element (P5 FR-PLUG-014/015/017/018/019). The shell checks the plugin was built for this
 * platform, loads its bundle only now (lazily, when the user navigates here), then mounts the element inside a shadow root: the plugin's styles
 * cannot reach the application and the application's cannot reach the plugin, while the theme's CSS custom properties still pass through.
 * If the plugin is incompatible or its bundle does not load, an inline error is shown instead of a broken shell.
 */
@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'app-plugin-element-page',
  imports: [TranslatePipe, ErrorState],
  styleUrl: './plugin-element-page.scss',
  templateUrl: './plugin-element-page.html'
})
export class PluginElementPage {
  private readonly route = inject(ActivatedRoute).snapshot.data as PluginElementRoute;
  private readonly loader = inject(PluginBundleLoader);
  private readonly auth = inject(AuthService);
  private readonly l10n = inject(LocalizationService);
  private readonly host = viewChild.required<ElementRef<HTMLElement>>('host');

  readonly state = signal<'loading' | 'ready' | 'incompatible' | 'failed'>('loading');

  constructor() {
    afterNextRender(() => void this.init());
  }

  /** Public so the error state's "Try again" can call it. */
  async init(): Promise<void> {
    if (compatibilityProblem(this.route)) {
      this.state.set('incompatible');
      return;
    }

    this.state.set('loading');
    try {
      await this.loader.load(this.address(this.route.entryUrl));
      if (!customElements.get(this.route.element)) throw new Error(`The bundle does not define <${this.route.element}>.`);
      this.mount();
      this.state.set('ready');
    } catch {
      this.state.set('failed');
    }
  }

  private mount(): void {
    const host = this.host().nativeElement;
    const shadow = host.shadowRoot ?? host.attachShadow({ mode: 'open' });
    shadow.replaceChildren();

    if (this.route.stylesUrl) {
      const styles = document.createElement('link');
      styles.rel = 'stylesheet';
      styles.href = this.address(this.route.stylesUrl);
      shadow.append(styles);
    }

    const element = document.createElement(this.route.element) as HTMLElement & { hostContext?: unknown };
    element.hostContext = {
      contract: this.route.contract,
      pluginId: this.route.pluginId,
      apiBaseUrl: environment.apiBaseUrl,
      language: this.l10n.language(),
      getAccessToken: () => this.auth.accessToken
    };
    shadow.append(element);
  }

  /** The manifest gives addresses relative to the API's own; make them absolute so a separately served frontend loads them from the API. */
  private address(path: string): string {
    return new URL(path, new URL(environment.apiBaseUrl, document.baseURI).origin).toString();
  }
}
