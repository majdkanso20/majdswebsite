import { HttpErrorResponse } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog';
import { of, throwError } from 'rxjs';
import { ImportDialog, ImportDialogData, ImportResult } from './import-dialog';

describe('ImportDialog', () => {
  const dialogRef = { close: vi.fn() };
  const data: ImportDialogData = { title: 'Import things', upload: vi.fn(), template: vi.fn() };
  const file = new File(['Email\na@b.co'], 'users.csv', { type: 'text/csv' });

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

  it('uploads the chosen file and lists each failed row with its reason', async () => {
    const result: ImportResult = { total: 3, succeeded: 2, failed: 1, errors: [{ row: 3, reason: 'Unknown role.' }] };
    (data.upload as ReturnType<typeof vi.fn>).mockReturnValue(of(result));
    const { fixture, dialog, element } = await create();

    dialog.file.set(file);
    dialog.import();
    fixture.detectChanges();

    expect(data.upload).toHaveBeenCalledWith(file);
    expect(element.querySelector('.import__summary')?.textContent).toContain('2');
    const cells = Array.from(element.querySelectorAll('.import__errors tbody td')).map((c) => c.textContent);
    expect(cells).toEqual(['3', 'Unknown role.']);
  });

  it('shows the reason when the whole file is rejected', async () => {
    const failure = new HttpErrorResponse({ status: 400, error: { errors: ['The file is missing the required column(s): Email.'] } });
    (data.upload as ReturnType<typeof vi.fn>).mockReturnValue(throwError(() => failure));
    const { fixture, dialog, element } = await create();

    dialog.file.set(file);
    dialog.import();
    fixture.detectChanges();

    expect(element.querySelector('.import__error')?.textContent).toContain('missing the required column');
    expect(dialog.uploading()).toBe(false);
  });

  it('tells the list to reload only when something was imported', async () => {
    const { dialog } = await create();

    dialog.close();
    expect(dialogRef.close).toHaveBeenLastCalledWith(false);

    dialog.result.set({ total: 1, succeeded: 1, failed: 0, errors: [] });
    dialog.close();
    expect(dialogRef.close).toHaveBeenLastCalledWith(true);
  });

  it('downloads the requested template', async () => {
    const { dialog } = await create();

    dialog.downloadTemplate('xlsx');

    expect(data.template).toHaveBeenCalledWith('xlsx');
  });
});
