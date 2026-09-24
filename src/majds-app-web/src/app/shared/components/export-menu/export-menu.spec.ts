import { TestBed } from '@angular/core/testing';
import { ExportFormat, exportFileName } from '../../../core/utils/download';
import { ExportMenu } from './export-menu';

describe('ExportMenu', () => {
  async function create() {
    const fixture = TestBed.createComponent(ExportMenu);
    await fixture.whenStable();
    fixture.detectChanges();
    return fixture;
  }

  it('offers CSV, Excel and PDF', async () => {
    const menu = (await create()).componentInstance;

    expect(menu.formats.map((f) => f.format)).toEqual(['csv', 'xlsx', 'pdf']);
  });

  it('lists the formats when opened and reports the one picked', async () => {
    const fixture = await create();
    const picked: ExportFormat[] = [];
    fixture.componentInstance.chosen.subscribe((f) => picked.push(f));

    (fixture.nativeElement as HTMLElement).querySelector<HTMLButtonElement>('button')!.click();
    fixture.detectChanges();
    await fixture.whenStable();

    const items = Array.from(document.querySelectorAll<HTMLButtonElement>('button[mat-menu-item]'));
    expect(items.map((i) => i.querySelector('span')?.textContent)).toEqual(['CSV', 'Excel', 'PDF']);
    items[1].click();
    expect(picked).toEqual(['xlsx']);
  });

  it('names the downloaded file after the format', () => {
    expect(exportFileName('users', 'xlsx')).toBe('users.xlsx');
    expect(exportFileName('audit-log', 'pdf')).toBe('audit-log.pdf');
  });
});
