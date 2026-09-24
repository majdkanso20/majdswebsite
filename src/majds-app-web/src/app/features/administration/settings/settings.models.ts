/** Mirrors the backend's SettingDataType enum (numeric — System.Text.Json's default for enums). */
export enum SettingDataType {
  String = 0,
  Boolean = 1,
  Integer = 2
}

export interface SettingDto {
  name: string;
  group: string;
  displayName: string;
  dataType: SettingDataType;
  value: string;
  defaultValue: string;
  description: string | null;
  /** Write-only secret: the API never returns its value, only whether one is set. */
  isSensitive: boolean;
  hasValue: boolean;
}

export interface SettingUpdateItem {
  name: string;
  value: string;
  clear?: boolean;
}
