import { TestBed } from '@angular/core/testing';
import { FeaturesService } from '../../../core/services/features.service';
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

  it('offers background exports only when the Files feature is on', async () => {
    const off = await create();
    expect(off.componentInstance.backgroundEnabled()).toBe(false);

    TestBed.inject(FeaturesService).isEnabled = () => true;
    const on = await create();
    const picked: string[] = [];
    on.componentInstance.background.subscribe((f) => picked.push(f));

    (on.nativeElement as HTMLElement).querySelector<HTMLButtonElement>('button')!.click();
    on.detectChanges();
    await on.whenStable();
    const items = Array.from(document.querySelectorAll<HTMLButtonElement>('button[mat-menu-item]'));
    items.find((i) => i.textContent?.includes('Excel') && i.querySelector('mat-icon')?.textContent === 'schedule')!.click();

    expect(on.componentInstance.backgroundEnabled()).toBe(true);
    expect(picked).toEqual(['xlsx']);
  });

  it('names the downloaded file after the format', () => {
    expect(exportFileName('users', 'xlsx')).toBe('users.xlsx');
    expect(exportFileName('audit-log', 'pdf')).toBe('audit-log.pdf');
  });
});
