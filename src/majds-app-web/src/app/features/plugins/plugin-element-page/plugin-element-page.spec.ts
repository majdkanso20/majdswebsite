import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute } from '@angular/router';
import { HOST_UI_CONTRACT, PluginBundleLoader, PluginElementRoute, compatibilityProblem } from '../../../core/plugins/plugin-ui';
import { PluginElementPage } from './plugin-element-page';

const route: PluginElementRoute = {
  pluginId: 'Acme.Demo',
  element: 'acme-demo-view',
  entryUrl: '/plugins/Acme.Demo/main.js',
  stylesUrl: '/plugins/Acme.Demo/styles.css',
  contract: HOST_UI_CONTRACT,
  angular: null
};

describe('compatibilityProblem (FR-PLUG-019)', () => {
  it('accepts a bundle built for this contract, with or without an Angular version', () => {
    expect(compatibilityProblem({ contract: HOST_UI_CONTRACT, angular: null }, 22)).toBeNull();
    expect(compatibilityProblem({ contract: HOST_UI_CONTRACT, angular: 22 }, 22)).toBeNull();
  });

  it('refuses another contract version or another Angular major version', () => {
    expect(compatibilityProblem({ contract: HOST_UI_CONTRACT + 1, angular: null }, 22)).toBe('contract');
    expect(compatibilityProblem({ contract: 0, angular: null }, 22)).toBe('contract');
    expect(compatibilityProblem({ contract: HOST_UI_CONTRACT, angular: 21 }, 22)).toBe('angular');
  });
});

describe('PluginElementPage', () => {
  let load: ReturnType<typeof vi.fn>;

  async function create(data: Partial<PluginElementRoute> = {}): Promise<ComponentFixture<PluginElementPage>> {
    load = vi.fn().mockResolvedValue(undefined);
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: ActivatedRoute, useValue: { snapshot: { data: { ...route, ...data } } } },
        { provide: PluginBundleLoader, useValue: { load } }
      ]
    });
    const fixture = TestBed.createComponent(PluginElementPage);
    await fixture.whenStable();
    await vi.waitFor(() => expect(fixture.componentInstance.state()).not.toBe('loading'));
    fixture.detectChanges();
    return fixture;
  }

  const host = (fixture: ComponentFixture<PluginElementPage>) =>
    (fixture.nativeElement as HTMLElement).querySelector('.plugin-element-page__host') as HTMLElement;

  afterEach(() => {
    document.body.replaceChildren();
  });

  it('loads the bundle, then mounts the element in a shadow root with the stylesheet and the host context', async () => {
    customElements.define('acme-demo-view', class extends HTMLElement {});
    const fixture = await create();

    expect(load).toHaveBeenCalledWith(expect.stringMatching(/\/plugins\/Acme\.Demo\/main\.js$/));
    expect(fixture.componentInstance.state()).toBe('ready');

    const shadow = host(fixture).shadowRoot!;
    expect(shadow.querySelector('link')!.getAttribute('href')).toMatch(/\/plugins\/Acme\.Demo\/styles\.css$/);
    const element = shadow.querySelector('acme-demo-view') as HTMLElement & { hostContext: { pluginId: string; contract: number; apiBaseUrl: string } };
    expect(element.hostContext.pluginId).toBe('Acme.Demo');
    expect(element.hostContext.contract).toBe(HOST_UI_CONTRACT);
    expect(element.hostContext.apiBaseUrl).toContain('/api');
    expect(host(fixture).querySelector('acme-demo-view')).toBeNull(); // it is inside the shadow root, not in the page
  });

  it('shows an inline message and loads nothing when the plugin was built for another contract', async () => {
    const fixture = await create({ contract: 99 });

    expect(fixture.componentInstance.state()).toBe('incompatible');
    expect(load).not.toHaveBeenCalled();
    expect(fixture.nativeElement.textContent).toContain('different version of the platform');
  });

  it('shows an inline error, not a blank page, when the bundle fails to load, and can try again', async () => {
    load = vi.fn();
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: ActivatedRoute, useValue: { snapshot: { data: { ...route, element: 'acme-late-view' } } } },
        { provide: PluginBundleLoader, useValue: { load } }
      ]
    });
    load.mockRejectedValueOnce(new Error('404')).mockImplementation(async () => {
      customElements.define('acme-late-view', class extends HTMLElement {});
    });
    const fixture = TestBed.createComponent(PluginElementPage);
    await fixture.whenStable();
    await vi.waitFor(() => expect(fixture.componentInstance.state()).toBe('failed'));
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('could not be loaded');

    await fixture.componentInstance.init();

    expect(fixture.componentInstance.state()).toBe('ready');
  });

  it('shows the error when the bundle loads but does not define the element', async () => {
    const fixture = await create({ element: 'acme-never-defined' });

    expect(fixture.componentInstance.state()).toBe('failed');
  });
});
