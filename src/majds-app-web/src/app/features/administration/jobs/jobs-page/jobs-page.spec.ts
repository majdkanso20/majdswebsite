import { TestBed } from '@angular/core/testing';
import { of, throwError } from 'rxjs';
import { PermissionService } from '../../../../core/services/permission.service';
import { BackgroundJobDto, JobsService } from '../jobs.service';
import { JobsPage } from './jobs-page';

const job = (over: Partial<BackgroundJobDto> = {}): BackgroundJobDto => ({
  id: 'j1', type: 'send-report', status: 'Failed', attempts: 3, maxAttempts: 3, userName: 'alice', lastError: 'smtp down',
  createdAt: '2026-09-25T10:00:00', nextAttemptAt: '2026-09-25T10:00:00', startedAt: null, completedAt: null, ...over
});

describe('JobsPage queue', () => {
  const api = { list: vi.fn(), runs: vi.fn(), queue: vi.fn(), retry: vi.fn(), delete: vi.fn(), run: vi.fn() };

  async function create(queued: BackgroundJobDto[]) {
    api.list.mockReturnValue(of([]));
    api.runs.mockReturnValue(of({ items: [], totalCount: 0, page: 1, pageSize: 10 }));
    api.queue.mockReturnValue(of({ items: queued, totalCount: queued.length, page: 1, pageSize: 10 }));
    const fixture = TestBed.createComponent(JobsPage);
    await fixture.whenStable();
    fixture.detectChanges();
    return { fixture, page: fixture.componentInstance, element: fixture.nativeElement as HTMLElement };
  }

  beforeEach(() => {
    Object.values(api).forEach((fn) => fn.mockReset());
    vi.spyOn(window, 'confirm').mockReturnValue(true);
    TestBed.configureTestingModule({
      providers: [
        { provide: JobsService, useValue: api },
        { provide: PermissionService, useValue: { has: () => true } }
      ]
    });
  });

  it('shows the queued jobs with their attempts and last error', async () => {
    const { page } = await create([job()]);

    expect(page.queued()).toHaveLength(1);
    const attempts = page.queueColumns.find((c) => c.key === 'attempts')!;
    expect(attempts.value!(job())).toBe('3 / 3');
    expect(page.queueColumns.find((c) => c.key === 'lastError')!.value!(job())).toBe('smtp down');
  });

  it('says when a pending job will next be tried', async () => {
    const { page } = await create([]);
    const status = page.queueColumns.find((c) => c.key === 'status')!;

    expect(status.value!(job({ status: 'Pending' }))).toContain('Pending, next try');
    expect(status.value!(job({ status: 'Succeeded' }))).toBe('Succeeded');
  });

  it('filters the queue by status and starts again from the first page', async () => {
    const { page } = await create([job()]);

    page.queueStatus = 'Failed';
    page.applyQueueFilter();

    expect(api.queue).toHaveBeenLastCalledWith(1, 10, 'Failed', 'createdAt:desc');
  });

  it('retries a failed job and reloads the queue', async () => {
    api.retry.mockReturnValue(of(undefined));
    const { page } = await create([job()]);

    page.retry(job());

    expect(api.retry).toHaveBeenCalledWith('j1');
    expect(api.queue).toHaveBeenCalledTimes(2);
  });

  it('deletes only after confirmation', async () => {
    api.delete.mockReturnValue(of(undefined));
    const { page } = await create([job()]);

    (window.confirm as ReturnType<typeof vi.fn>).mockReturnValue(false);
    page.remove(job());
    expect(api.delete).not.toHaveBeenCalled();

    (window.confirm as ReturnType<typeof vi.fn>).mockReturnValue(true);
    page.remove(job());
    expect(api.delete).toHaveBeenCalledWith('j1');
  });

  it('keeps the list and reports the reason when the server refuses (for example deleting a running job)', async () => {
    api.delete.mockReturnValue(throwError(() => ({ error: { message: 'A running job cannot be deleted.' } })));
    const { page } = await create([job({ status: 'Running' })]);

    page.remove(job({ status: 'Running' }));

    expect(api.queue).toHaveBeenCalledTimes(1);
  });

  it('shows the cron expression for a cron job and the failure streak for a failing one', async () => {
    const { page } = await create([]);
    const every = page.jobColumns.find((c) => c.key === 'intervalMinutes')!;
    const result = page.jobColumns.find((c) => c.key === 'lastSuccess')!;
    const base = { name: 'n', intervalMinutes: 60, lastRunAt: null, nextRunAt: null };

    expect(every.value!({ ...base, cron: '0 3 * * *', lastSuccess: null, consecutiveFailures: 0 })).toBe('0 3 * * *');
    expect(every.value!({ ...base, cron: null, lastSuccess: null, consecutiveFailures: 0 })).toBe('1 h');
    expect(result.value!({ ...base, cron: null, lastSuccess: false, consecutiveFailures: 3 })).toBe('Failed (3 in a row)');
    expect(result.value!({ ...base, cron: null, lastSuccess: true, consecutiveFailures: 0 })).toBe('OK');
  });
});
