import { TestBed } from '@angular/core/testing';
import { of, throwError } from 'rxjs';
import { DashboardService, DashboardWidget } from '../../../core/services/dashboard.service';
import { Dashboard } from './dashboard';

const widget = (key: string, visible = true): DashboardWidget => ({ key, title: key, kind: 'kpi', columns: 1, visible });

describe('Dashboard', () => {
  const api = { widgets: vi.fn(), data: vi.fn(), saveLayout: vi.fn() };

  async function createPage(widgets: DashboardWidget[]) {
    api.widgets.mockReturnValue(of(widgets));
    const fixture = TestBed.createComponent(Dashboard);
    await fixture.whenStable();
    fixture.detectChanges();
    return { fixture, page: fixture.componentInstance };
  }

  beforeEach(() => {
    api.widgets.mockReset();
    api.data.mockReset().mockReturnValue(of({ value: '1', caption: null }));
    api.saveLayout.mockReset().mockReturnValue(of(null));
    TestBed.configureTestingModule({ providers: [{ provide: DashboardService, useValue: api }] });
  });

  it('shows only the widgets the server returned and the user has not hidden', async () => {
    const { page } = await createPage([widget('a'), widget('b', false), widget('c')]);

    expect(page.visible().map((w) => w.key)).toEqual(['a', 'c']);
  });

  it('lists a widget it has never heard of, without any change to the dashboard', async () => {
    const { page } = await createPage([widget('brand-new-widget')]);

    expect(page.visible().map((w) => w.key)).toEqual(['brand-new-widget']);
  });

  it('shows a message when there is nothing to display', async () => {
    const { fixture } = await createPage([]);

    expect((fixture.nativeElement as HTMLElement).querySelector('app-empty-state')).not.toBeNull();
  });

  it('reports an unreachable API instead of an empty dashboard', async () => {
    api.widgets.mockReturnValue(throwError(() => new Error('offline')));
    const fixture = TestBed.createComponent(Dashboard);
    await fixture.whenStable();

    expect(fixture.componentInstance.loadFailed()).toBe(true);
  });

  it('hides a widget and saves the new layout', async () => {
    const { page } = await createPage([widget('a'), widget('b')]);

    page.openCustomize();
    page.toggle(1);
    page.save();

    expect(api.saveLayout).toHaveBeenCalledWith([widget('a'), widget('b', false)], false);
    expect(page.visible().map((w) => w.key)).toEqual(['a']);
    expect(page.customizing()).toBe(false);
  });

  it('reorders widgets and keeps that order when saved', async () => {
    const { page } = await createPage([widget('a'), widget('b'), widget('c')]);

    page.openCustomize();
    page.move(2, -1);
    page.move(0, -1); // already first: no change
    page.save();

    expect(page.visible().map((w) => w.key)).toEqual(['a', 'c', 'b']);
  });

  it('cancelling customization leaves the layout untouched', async () => {
    const { page } = await createPage([widget('a'), widget('b')]);

    page.openCustomize();
    page.toggle(0);
    page.closeCustomize();

    expect(api.saveLayout).not.toHaveBeenCalled();
    expect(page.visible().map((w) => w.key)).toEqual(['a', 'b']);
  });

  it('resetting drops the personal layout and reloads the default one', async () => {
    const { page } = await createPage([widget('b'), widget('a', false)]);
    api.widgets.mockReturnValue(of([widget('a'), widget('b')]));

    page.openCustomize();
    page.reset();

    expect(api.saveLayout).toHaveBeenCalledWith([], true);
    expect(page.visible().map((w) => w.key)).toEqual(['a', 'b']);
  });
});
