import { authenticatedFetch } from '../../auth/api';

export type ReportTargetType = 'User' | 'Message';
export type CreateReportRequest = { targetType: ReportTargetType; targetId: string; reason: string; description: string };
export type CreatedReport = { id: string; reporterUserId: string; targetType: ReportTargetType; targetId: string;
  targetUserId: string; reason: string; description: string; status: 'Pending'; createdAtUtc: string };

export function createReport(input: CreateReportRequest): Promise<CreatedReport> {
  // Whitelist fields: caller-supplied reporter/status must never choose the actor or review result.
  const { targetType, targetId, reason, description } = input;
  return authenticatedFetch('/api/v1/reports', { method: 'POST', headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ targetType, targetId: targetId.trim(), reason: reason.trim(), description: description.trim() }) });
}
