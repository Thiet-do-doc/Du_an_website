export type Interest = { id: string; name: string };
export type ProfileConnection = { state: 'self' | 'none' | 'received' | 'sent' | 'matched' | 'unmatched' | 'needsProfile'; conversationId: string | null };
export type PublicProfile = Omit<Profile, 'isDiscoverable' | 'isComplete'> & { connection: ProfileConnection };
export type Photo = { id: string; url: string; isAvatar: boolean; sortOrder: number };
export type Profile = {
  userId: string; displayName: string; age: number; bio: string | null; city: string | null;
  gender: string | null; isDiscoverable: boolean; isComplete: boolean; photos: Photo[]; interests: Interest[];
};
