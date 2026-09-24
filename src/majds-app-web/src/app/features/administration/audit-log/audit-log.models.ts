export interface AuditLogEntryDto {
  id: number;
  action: string;
  userName: string | null;
  createdAt: string;
  outcome: string;
  succeeded: boolean;
  durationMs: number;
  clientIp: string | null;
  httpMethod: string | null;
  url: string | null;
}

export interface AuditPropertyChangeDto {
  property: string;
  original: string | null;
  new: string | null;
}

export interface AuditEntityChangeDto {
  entityType: string;
  entityId: string;
  changeType: string;
  properties: AuditPropertyChangeDto[];
}

export interface AuditLogEntryDetailDto extends AuditLogEntryDto {
  userId: string | null;
  clientBrowser: string | null;
  error: string | null;
  parameters: string | null;
  changes: AuditEntityChangeDto[];
}

export interface AuditLogFilterParams {
  from?: string;
  to?: string;
  outcome?: string;
}
