import { HttpErrorResponse } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { MatDialogRef } from '@angular/material/dialog';
import { of, throwError } from 'rxjs';
import { PluginChangeDto, PluginsApiService } from '../plugins-api.service';
import { PluginInstallDialog } from './plugin-install-dialog';

describe('PluginInstallDialog', () => {
  const api = { install: vi.fn() };
  const dialogRef = { close: vi.fn() };
  const file = new File(['zip'], 'plugin.zip', { type: 'application/zip' });
  const staged: PluginChangeDto = { id: 'Acme.Tasks', name: 'Tasks', version: '2.0.0', action: 'Upgrade', previousVersion: '1.0.0', sha256: 'ab12', restartRequired: true };

  function create() {
    const fixture = TestBed.createComponent(PluginInstallDialog);
    fixture.detectChanges();
    return { fixture, dialog: fixture.componentInstance, element: fixture.nativeElement as HTMLElement };
  }

  beforeEach(() => {
    api.install.mockReset();
    dialogRef.close.mockReset();
    TestBed.configureTestingModule({
      providers: [
        { provide: PluginsApiService, useValue: api },
        { provide: MatDialogRef, useValue: dialogRef }
      ]
    });
  });

  it('cannot install until a package has been chosen', () => {
    const { dialog } = create();

    dialog.install();

    expect(api.install).not.toHaveBeenCalled();
  });

  it('sends the package and the publisher checksum, then shows what will happen and the package checksum', () => {
    api.install.mockReturnValue(of(staged));
    const { fixture, dialog, element } = create();

    dialog.file.set(file);
    dialog.checksum = ' ab12 ';
    dialog.install();
    fixture.detectChanges();

    expect(api.install).toHaveBeenCalledWith(file, ' ab12 ');
    expect(element.querySelector('.plugin-install__result')?.textContent).toContain('Tasks 2.0.0');
    expect(element.querySelector('.plugin-install__sha')?.textContent).toContain('ab12');
    expect(element.querySelector('.plugin-install__result')?.textContent).toContain('1.0.0');
  });

  it('shows the reason when the package is refused', () => {
    api.install.mockReturnValue(throwError(() => new HttpErrorResponse({ status: 400, error: { errors: ['The package does not match the checksum you provided.'] } })));
    const { fixture, dialog, element } = create();

    dialog.file.set(file);
    dialog.install();
    fixture.detectChanges();

    expect(element.querySelector('.plugin-install__error')?.textContent).toContain('checksum');
    expect(dialog.uploading()).toBe(false);
    expect(dialog.result()).toBeNull();
  });

  it('tells the list to reload only when something was staged', () => {
    const { dialog } = create();

    dialog.close();
    expect(dialogRef.close).toHaveBeenLastCalledWith(false);

    dialog.result.set(staged);
    dialog.close();
    expect(dialogRef.close).toHaveBeenLastCalledWith(true);
  });
});
