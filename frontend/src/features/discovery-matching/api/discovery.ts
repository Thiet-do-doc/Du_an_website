import type { DiscoveryFilters, DiscoveryPage, LikeResult, MatchSummary } from '../types/discovery';
import { authenticatedFetch } from '../../auth/api';

async function request<T>(path: string, init?: RequestInit): Promise<T> {
  const headers = new Headers(init?.headers);
  headers.set('Accept', 'application/json');
  return authenticatedFetch<T>(path, { ...init, headers });
}

export async function getDiscover(filters: DiscoveryFilters, page: number, signal?: AbortSignal): Promise<DiscoveryPage> {
  const params = new URLSearchParams({ page: String(page), pageSize: '20' });
  if (filters.minAge !== 18) params.set('minAge', String(filters.minAge));
  if (filters.maxAge !== 35) params.set('maxAge', String(filters.maxAge));
  if (filters.city !== 'Tất cả khu vực') params.set('city', filters.city);
  if (filters.interest !== 'Tất cả sở thích') params.set('interest', filters.interest);
  const result = await request<{ items: Array<{ userId: string; displayName: string; age: number; city: string; occupation?: string | null; bio?: string | null; interests: string[]; photoUrl?: string | null; distanceKm?: number | null }>; page: number; pageSize: number; hasMore: boolean }>(`/api/v1/discover?${params}`, { signal });
  return {
    ...result,
    items: result.items.map(profile => ({
      id: profile.userId,
      name: profile.displayName,
      age: profile.age,
      city: profile.city,
      occupation: profile.occupation ?? '',
      bio: profile.bio ?? '',
      interests: profile.interests,
      photoUrl: profile.photoUrl ?? '',
      distanceKm: profile.distanceKm ?? null,
    })),
  };
}

export function likeProfile(userId: string): Promise<LikeResult> {
  return request(`/api/v1/user-likes/${encodeURIComponent(userId)}`, { method: 'POST' });
}

export function passProfile(userId: string): Promise<void> {
  return request(`/api/v1/passes/${encodeURIComponent(userId)}`, { method: 'POST' });
}

export function getMatches(): Promise<MatchSummary[]> {
  return request<Array<{ id: string; otherUserId: string; conversationId: string; matchedAt: string; isActive: boolean; otherProfile: { userId: string; displayName: string; age: number; city: string; occupation?: string | null; bio?: string | null; interests: string[]; photoUrl?: string | null; distanceKm?: number | null } }>>('/api/v1/matches').then(items => items.map(match => ({
    id: match.id,
    otherUserId: match.otherUserId,
    conversationId: match.conversationId,
    matchedAt: match.matchedAt,
    isActive: match.isActive,
    otherProfile: {
      id: match.otherProfile.userId,
      name: match.otherProfile.displayName,
      age: match.otherProfile.age,
      city: match.otherProfile.city,
      occupation: match.otherProfile.occupation ?? '',
      bio: match.otherProfile.bio ?? '',
      interests: match.otherProfile.interests,
      photoUrl: match.otherProfile.photoUrl ?? '',
      distanceKm: match.otherProfile.distanceKm ?? null,
    },
  })));
}

export function unmatch(matchId: string): Promise<void> {
  return request(`/api/v1/matches/${encodeURIComponent(matchId)}`, { method: 'DELETE' });
}
