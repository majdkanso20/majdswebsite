import { HttpErrorResponse } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog';
import { of, throwError } from 'rxjs';
import { ImportJob } from '../../../core/services/imports.service';
import { ImportDialog, ImportDialogData } from './import-dialog';

describe('ImportDialog', () => {
  const dialogRef = { close: vi.fn() };
  const data: ImportDialogData = { title: 'Import things', upload: vi.fn(), template: vi.fn() };
  const file = new File(['Email\na@b.co'], 'users.csv', { type: 'text/csv' });

  const job: ImportJob = {
    id: 'job-1', source: 'users', title: 'Users', fileName: 'users.csv', status: 'Pending',
    total: 0, succeeded: 0, errors: null, error: null, createdAt: '2026-01-01T00:00:00Z', completedAt: null
  };

  async function create() {
    const fixture = TestBed.createComponent(ImportDialog);
    await fixture.whenStable();
    fixture.detectChanges();
    return { fixture, dialog: fixture.componentInstance, element: fixture.nativeElement as HTMLElement };
  }

  beforeEach(() => {
    dialogRef.close.mockReset();
    (data.upload as ReturnType<typeof vi.fn>).mockReset();
    (data.template as ReturnType<typeof vi.fn>).mockReset().mockReturnValue(of(undefined));
    TestBed.configureTestingModule({
      providers: [
        { provide: MAT_DIALOG_DATA, useValue: data },
        { provide: MatDialogRef, useValue: dialogRef }
      ]
    });
  });

  it('cannot import until a file has been chosen', async () => {
    const { dialog } = await create();

    dialog.import();

    expect(data.upload).not.toHaveBeenCalled();
  });

  it('uploads the chosen file and confirms it was queued, without waiting for it to finish', async () => {
    (data.upload as ReturnType<typeof vi.fn>).mockReturnValue(of(job));
    const { fixture, dialog, element } = await create();

    dialog.file.set(file);
    dialog.import();
    fixture.detectChanges();

    expect(data.upload).toHaveBeenCalledWith(file);
    expect(dialog.queued()).toEqual(job);
    expect(element.querySelector('.import__queued')?.textContent).toContain('queued');
    expect(element.querySelector('.import__file')).toBeNull(); // the form is replaced by the confirmation, not shown alongside it
  });

  it('shows the reason when queuing itself is refused (too large, too many in progress...)', async () => {
    const failure = new HttpErrorResponse({ status: 400, error: { errors: ['The file must be no larger than 5 MB.'] } });
    (data.upload as ReturnType<typeof vi.fn>).mockReturnValue(throwError(() => failure));
    const { fixture, dialog, element } = await create();

    dialog.file.set(file);
    dialog.import();
    fixture.detectChanges();

    expect(element.querySelector('.import__error')?.textContent).toContain('no larger than 5 MB');
    expect(dialog.uploading()).toBe(false);
  });

  it('tells the list something was queued only once it actually was', async () => {
    const { dialog } = await create();

    dialog.close();
    expect(dialogRef.close).toHaveBeenLastCalledWith(false);

    dialog.queued.set(job);
    dialog.close();
    expect(dialogRef.close).toHaveBeenLastCalledWith(true);
  });

  it('downloads the requested template', async () => {
    const { dialog } = await create();

    dialog.downloadTemplate('xlsx');

    expect(data.template).toHaveBeenCalledWith('xlsx');
  });
});
