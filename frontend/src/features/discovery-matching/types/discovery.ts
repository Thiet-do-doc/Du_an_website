export type DiscoveryProfile = {
  id: string;
  name: string;
  age: number;
  city: string;
  occupation: string;
  bio: string;
  interests: string[];
  photoUrl: string;
  distanceKm: number | null;
};

export type DiscoveryFilters = {
  minAge: number;
  maxAge: number;
  city: string;
  interest: string;
};

export type DiscoveryPage = {
  items: DiscoveryProfile[];
  page: number;
  pageSize: number;
  hasMore: boolean;
};

export type LikeResult = {
  isMatch: boolean;
  matchId: string | null;
  conversationId: string | null;
  matchedAt: string | null;
};

export type MatchSummary = {
  id: string;
  otherUserId: string;
  conversationId: string;
  matchedAt: string;
  isActive: boolean;
  otherProfile: DiscoveryProfile;
};
