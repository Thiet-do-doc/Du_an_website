import type { BlockedUser } from '../types/block';

const baseUrl = '/api/v1/blocks';

function authHeaders(accessToken: string): HeadersInit {
  return { Authorization: `Bearer ${accessToken}` };
}

async function ensureSuccess(response: Response): Promise<void> {
  if (!response.ok) throw new Error(`Không thể cập nhật danh sách chặn (HTTP ${response.status}).`);
}

export async function getBlockedUsers(accessToken: string): Promise<BlockedUser[]> {
  const response = await fetch(baseUrl, { headers: authHeaders(accessToken) });
  await ensureSuccess(response);
  return response.json() as Promise<BlockedUser[]>;
}

export async function blockUser(accessToken: string, userId: string): Promise<void> {
  const response = await fetch(`${baseUrl}/${encodeURIComponent(userId)}`, {
    method: 'PUT', headers: authHeaders(accessToken),
  });
  await ensureSuccess(response);
}

export async function unblockUser(accessToken: string, userId: string): Promise<void> {
  const response = await fetch(`${baseUrl}/${encodeURIComponent(userId)}`, {
    method: 'DELETE', headers: authHeaders(accessToken),
  });
  await ensureSuccess(response);
}
