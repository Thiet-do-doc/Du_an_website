import type { ChatNotification, ConversationPage, MessagePage, NotificationPage } from '../types/chat';

export async function api<T>(path: string, token: string, options: RequestInit = {}): Promise<T> {
  const headers = new Headers(options.headers);
  if (options.body) headers.set('Content-Type', 'application/json');
  headers.set('Authorization', `Bearer ${token}`);
  const response = await fetch(path, { ...options, headers });
  if (!response.ok) {
    const problem = await response.json().catch(() => null) as { title?: string } | null;
    throw new Error(problem?.title ?? `HTTP ${response.status}: yêu cầu thất bại`);
  }
  return await response.json() as T;
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

export const getNotifications = (token: string) =>
  api<NotificationPage>('/api/v1/notifications?page=1&pageSize=50', token);

export const markRead = (token: string, id: string) =>
  api<ChatNotification>(`/api/v1/notifications/${id}/read`, token, { method: 'PATCH' });
