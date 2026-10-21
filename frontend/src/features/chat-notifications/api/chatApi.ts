import { authenticatedFetch } from '../../auth/api';
import type { ChatNotification, ConversationPage, MessagePage, NotificationPage } from '../types/chat';

// Keep the token argument for the ChatPage boundary, but delegate auth/refresh/retry to the
// shared AuthenticatedFetch instead of creating a parallel token implementation in TV4.
export async function api<T>(path: string, _token: string, options: RequestInit = {}): Promise<T> {
  const headers = new Headers(options.headers);
  if (options.body && !headers.has('Content-Type')) headers.set('Content-Type', 'application/json');
  return authenticatedFetch<T>(path, { ...options, headers });
}

export const getConversations = (token: string) =>
  api<ConversationPage>('/api/v1/conversations?page=1&pageSize=50', token);

export const getMessages = (token: string, conversationId: string,
  opts: { cursor?: string | null; afterId?: string | null } = {}) => {
  const query = new URLSearchParams({ limit: '30' });
  if (opts.cursor) query.set('cursor', opts.cursor);
  if (opts.afterId) query.set('afterId', opts.afterId);
  return api<MessagePage>(`/api/v1/conversations/${conversationId}/messages?${query}`, token);
};

export const getNotifications = (token: string,
  opts: { cursor?: string | null; afterId?: string | null; pageSize?: number } = {}) => {
  const query = new URLSearchParams({ page: '1', pageSize: String(opts.pageSize ?? 50) });
  if (opts.cursor) query.set('cursor', opts.cursor);
  if (opts.afterId) query.set('afterId', opts.afterId);
  return api<NotificationPage>(`/api/v1/notifications?${query}`, token);
};

export const markRead = (token: string, id: string) =>
  api<ChatNotification>(`/api/v1/notifications/${id}/read`, token, { method: 'PATCH' });

// The shared SafetyAdmin endpoint must validate the selected message's existence and authorization.
// Until that target validator is merged by TV5, 404/501 is surfaced as an integration blocker, never success.
export const reportMessage = (token: string, messageId: string, reason: string, description: string) =>
  api<{ id: string }>('/api/v1/reports', token, {
    method: 'POST',
    body: JSON.stringify({ targetType: 'Message', targetId: messageId, reason, description }),
  });
