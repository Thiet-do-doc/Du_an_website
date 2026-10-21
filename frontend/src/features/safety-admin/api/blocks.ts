import { authenticatedFetch } from '../../auth/api';
import type { BlockedUser } from '../types/block';
const baseUrl = '/api/v1/blocks';
export const getBlockedUsers = () => authenticatedFetch<BlockedUser[]>(baseUrl);
export const blockUser = (userId: string) => authenticatedFetch<void>(`${baseUrl}/${encodeURIComponent(userId)}`, { method: 'POST' });
export const unblockUser = (userId: string) => authenticatedFetch<void>(`${baseUrl}/${encodeURIComponent(userId)}`, { method: 'DELETE' });
