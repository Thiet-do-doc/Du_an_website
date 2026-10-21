import { authenticatedFetch } from '../auth/api';
import type { Interest, Profile, Photo } from './types';

export const getMyProfile = () => authenticatedFetch<Profile>('/api/v1/profiles/me');
export const getInterests = () => authenticatedFetch<Interest[]>('/api/v1/interests');
export const saveProfile = (value: { displayName: string; bio: string; city: string; gender: string;
  isDiscoverable: boolean; interestIds: string[] }) => authenticatedFetch<Profile>('/api/v1/profiles/me', {
  method: 'PUT', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(value),
});
export async function uploadPhoto(file: File): Promise<Photo> {
  const body = new FormData(); body.set('file', file);
  return authenticatedFetch<Photo>('/api/v1/photos', { method: 'POST', body });
}
export const setAvatar = (id: string) => authenticatedFetch<void>(`/api/v1/photos/${id}/avatar`, { method: 'PUT' });
export const deletePhoto = (id: string) => authenticatedFetch<void>(`/api/v1/photos/${id}`, { method: 'DELETE' });
export const reorderPhotos = (photoIds: string[]) => authenticatedFetch<void>('/api/v1/photos/order', {
  method: 'PUT', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ photoIds }),
});
