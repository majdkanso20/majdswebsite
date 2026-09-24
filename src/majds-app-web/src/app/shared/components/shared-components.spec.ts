import { Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { DataGrid } from './data-grid/data-grid';
import { GridColumn } from './data-grid/data-grid.model';
import { ErrorState } from './error-state/error-state';

interface Person {
  name: string;
  email: string;
}

@Component({
  imports: [DataGrid],
  template: `
    <app-data-grid [columns]="columns" [rows]="rows" [totalCount]="rows.length" (page)="pages.push($event.pageIndex)" (sortChange)="sorts.push($event.active)">
      <ng-template #cellTemplate let-row let-column="column">
        @if (column.key === 'email') {
          <b class="custom-cell">{{ row.email.toUpperCase() }}</b>
        } @else {
          {{ row[column.key] }}
        }
      </ng-template>
      <ng-template #actionsTemplate let-row>
        <button class="row-action" (click)="clicked.push(row.name)">Edit</button>
      </ng-template>
    </app-data-grid>
  `
})
class GridHost {
  columns: GridColumn<Person>[] = [
    { key: 'name', header: 'Name', sortable: true },
    { key: 'email', header: 'Email' }
  ];
  rows: Person[] = [
    { name: 'Ada', email: 'ada@example.com' },
    { name: 'Grace', email: 'grace@example.com' }
  ];
  pages: number[] = [];
  sorts: string[] = [];
  clicked: string[] = [];
}

describe('DataGrid', () => {
  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
  });

  async function render() {
    const fixture = TestBed.createComponent(GridHost);
    await fixture.whenStable();
    fixture.detectChanges();
    return { fixture, element: fixture.nativeElement as HTMLElement };
  }

  it('renders one row per record with the column headers', async () => {
    const { element } = await render();

    expect(element.querySelectorAll('tbody tr')).toHaveLength(2);
    expect([...element.querySelectorAll('th')].map((th) => th.textContent?.trim()).slice(0, 2)).toEqual(['Name', 'Email']);
  });

  it('lets the consumer replace how a cell is drawn without touching the grid (FR-UI-005)', async () => {
    const { element } = await render();

    expect(element.querySelector('.custom-cell')?.textContent).toBe('ADA@EXAMPLE.COM');
  });

  it('projects consumer row actions and keeps them wired to the consumer', async () => {
    const { fixture, element } = await render();

    (element.querySelector('.row-action') as HTMLButtonElement).click();

    expect(fixture.componentInstance.clicked).toEqual(['Ada']);
  });

  it('labels every cell so the narrow-container card layout can show what each value is (FR-MOB-001)', async () => {
    const { element } = await render();

    const cells = [...element.querySelectorAll('tbody tr:first-child td')].slice(0, 2);
    expect(cells.map((c) => c.getAttribute('data-label'))).toEqual(['Name', 'Email']);
  });

  it('shows the empty state instead of a blank table when there are no rows', async () => {
    const fixture = TestBed.createComponent(GridHost);
    fixture.componentInstance.rows = [];
    await fixture.whenStable();
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('app-empty-state')).not.toBeNull();
  });
});

describe('ErrorState', () => {
  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
  });

  it('announces itself to assistive technology and offers a retry that the page can handle', async () => {
    const fixture = TestBed.createComponent(ErrorState);
    const retried = vi.fn();
    fixture.componentInstance.retry.subscribe(retried);
    await fixture.whenStable();
    fixture.detectChanges();
    const element = fixture.nativeElement as HTMLElement;

    expect(element.querySelector('[role="alert"]')).not.toBeNull();
    (element.querySelector('button') as HTMLButtonElement).click();

    expect(retried).toHaveBeenCalledTimes(1);
  });

  it('hides the retry button when retrying makes no sense', async () => {
    const fixture = TestBed.createComponent(ErrorState);
    fixture.componentRef.setInput('retryable', false);
    await fixture.whenStable();
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('button')).toBeNull();
  });
});
