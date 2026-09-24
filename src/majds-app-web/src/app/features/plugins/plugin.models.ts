export interface PluginUiField {
  key: string;
  label: string;
  type: 'text' | 'textarea' | 'select' | 'date';
  required: boolean;
  options?: string[] | null;
}

export interface PluginUiColumn {
  key: string;
  header: string;
}

export interface PluginUiSchema {
  title: string;
  columns: PluginUiColumn[];
  fields: PluginUiField[];
  viewPermission: string;
  createPermission: string;
  editPermission: string;
  deletePermission: string;
}

export type PluginRecord = Record<string, unknown> & { id: string };
