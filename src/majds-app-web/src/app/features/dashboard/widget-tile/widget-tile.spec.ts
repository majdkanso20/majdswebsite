import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { LocalizationService } from '../../../core/i18n/localization.service';
import { DashboardService, DashboardWidget } from '../../../core/services/dashboard.service';
import { WidgetTile } from './widget-tile';

const widget: DashboardWidget = { key: 'users.total', title: 'Users', kind: 'kpi', columns: 1, visible: true };

describe('WidgetTile', () => {
  const language = signal('en');
  const api = { data: vi.fn() };

  async function create() {
    TestBed.configureTestingModule({
      providers: [
        { provide: DashboardService, useValue: api },
        { provide: LocalizationService, useValue: { language, translate: (key: string) => key } }
      ]
    });
    const fixture = TestBed.createComponent(WidgetTile);
    fixture.componentRef.setInput('widget', widget);
    await fixture.whenStable();
    fixture.detectChanges();
    return { fixture, element: fixture.nativeElement as HTMLElement };
  }

  beforeEach(() => {
    language.set('en');
    api.data.mockReset().mockReturnValue(of({ value: '6', caption: '6 active' }));
  });

  it('loads its own data once when it appears', async () => {
    const { element } = await create();

    expect(api.data).toHaveBeenCalledTimes(1);
    expect(element.textContent).toContain('6 active');
  });

  it('asks the server again when the language changes, because some of the wording comes from the server', async () => {
    const { fixture, element } = await create();
    api.data.mockReturnValue(of({ value: '6', caption: '6 نشط' }));

    language.set('ar');
    await fixture.whenStable();
    fixture.detectChanges();

    expect(api.data).toHaveBeenCalledTimes(2);
    expect(element.textContent).toContain('6 نشط');
  });
});
