export interface GridColumn<T> {
  key: string;
  header: string;
  sortable?: boolean;
  /** Default plain-text accessor; ignored if the consumer supplies a cellTemplate for this column. */
  value?: (row: T) => string;
}

export interface GridSort {
  active: string;
  direction: 'asc' | 'desc' | '';
}

export interface GridPage {
  pageIndex: number;
  pageSize: number;
}
