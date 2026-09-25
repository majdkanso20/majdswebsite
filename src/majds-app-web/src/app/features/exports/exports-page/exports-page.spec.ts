import { TestBed } from '@angular/core/testing';
import { of, throwError } from 'rxjs';
import { ExportJob, ExportsService, activeFilters } from '../../../core/services/exports.service';
import { ExportsPage } from './exports-page';

const job = (over: Partial<ExportJob>): ExportJob => ({
  id: 'j1', source: 'users', title: 'Users', format: 'xlsx', status: 'Completed', fileId: 'f1', fileName: 'users.xlsx',
  size: 2048, error: null, createdAt: '2026-09-25T10:00:00', completedAt: '2026-09-25T10:00:05', ...over
});

describe('ExportsPage', () => {
  const api = { list: vi.fn(), download: vi.fn(), start: vi.fn() };

  async function create(jobs: ExportJob[]) {
    api.list.mockReturnValue(of(jobs));
    const fixture = TestBed.createComponent(ExportsPage);
    fixture.detectChanges(); // ngOnInit loads synchronously: the mocked API answers at once
    return { fixture, page: fixture.componentInstance, element: fixture.nativeElement as HTMLElement };
  }

  beforeEach(() => {
    api.list.mockReset();
    api.download.mockReset().mockReturnValue(of(undefined));
    TestBed.configureTestingModule({ providers: [{ provide: ExportsService, useValue: api }] });
  });

  afterEach(() => vi.useRealTimers());

  it('offers a download only for finished exports', async () => {
    const { element } = await create([job({}), job({ id: 'j2', status: 'Failed', fileId: null, error: 'boom' })]);

    expect(element.querySelectorAll('.exports__item').length).toBe(2);
    expect(element.querySelectorAll('.exports__item button').length).toBe(1);
    expect(element.querySelector('.exports__error')?.textContent).toContain('boom');
  });

  it('shows a helpful message when there are no exports', async () => {
    const { element } = await create([]);

    expect(element.querySelector('app-empty-state')).not.toBeNull();
  });

  it('keeps refreshing while an export is running and stops once everything is finished', async () => {
    vi.useFakeTimers();
    const { page } = await create([job({ status: 'Running', fileId: null })]);
    expect(api.list).toHaveBeenCalledTimes(1);
    expect(page.hasActive()).toBe(true);

    api.list.mockReturnValue(of([job({})]));
    await vi.advanceTimersByTimeAsync(3100);
    expect(api.list).toHaveBeenCalledTimes(2);
    expect(page.hasActive()).toBe(false);

    await vi.advanceTimersByTimeAsync(10000);
    expect(api.list).toHaveBeenCalledTimes(2); // nothing active: no more polling
  });

  it('downloads the chosen export', async () => {
    const finished = job({});
    const { page } = await create([finished]);

    page.download(finished);

    expect(api.download).toHaveBeenCalledWith(finished);
  });

  it('shows an empty list rather than an error page when loading fails', async () => {
    api.list.mockReturnValue(throwError(() => new Error('offline')));
    const fixture = TestBed.createComponent(ExportsPage);
    fixture.detectChanges();

    expect(fixture.componentInstance.jobs()).toEqual([]);
  });
});

describe('activeFilters', () => {
  it('sends only the filters that have a value', () => {
    expect(activeFilters({ filter: 'ada', role: '', isActive: false, from: undefined, to: null })).toEqual({ filter: 'ada', isActive: 'false' });
  });
});
