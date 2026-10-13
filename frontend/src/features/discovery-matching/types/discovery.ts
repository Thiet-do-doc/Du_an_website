export type DiscoveryProfile = {
  id: string;
  name: string;
  age: number;
  city: string;
  occupation: string;
  bio: string;
  interests: string[];
  photoUrl: string;
  distanceKm: number;
};

export type DiscoveryFilters = {
  minAge: number;
  maxAge: number;
  city: string;
  interest: string;
};

export type Match = {
  id: string;
  profile: DiscoveryProfile;
  matchedAt: string;
};
