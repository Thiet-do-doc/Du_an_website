import { useEffect, useState } from 'react';
import { getPublicProfile, getPrivatePhoto } from '../../profiles/api';
import type { Conversation } from '../types/chat';

export function useConversationAvatars(conversations: Conversation[], viewerId: string) {
  const partnerKey = [...new Set(conversations.filter(c => !c.blockedByPartner).map(c => c.partnerId))]
    .sort().join(',');
  const [avatars, setAvatars] = useState<{ viewerId: string; sources: ReadonlyMap<string, string> }>();

  useEffect(() => {
    const controller = new AbortController();
    const photos = new Map<string, { url: string; source: string }>();
    const partners = partnerKey ? partnerKey.split(',') : [];
    let refreshing = false;

    function publish() {
      setAvatars({ viewerId, sources: new Map([...photos].map(([id, photo]) => [id, photo.source])) });
    }

    async function loadAvatar(id: string) {
      const previous = photos.get(id);
      const signal = AbortSignal.any([controller.signal, AbortSignal.timeout(15_000)]);
      let next: typeof previous;
      try {
        const profile = await getPublicProfile(id, signal);
        const url = profile.photos.find(photo => photo.isAvatar)?.url;
        if (url) {
          if (previous?.url === url) return;
          const blob = await getPrivatePhoto(url, signal);
          if (controller.signal.aborted) return;
          next = { url, source: URL.createObjectURL(blob) };
        }
      } catch {
        // An unavailable profile or photo falls back to the conversation initials.
      }
      if (controller.signal.aborted || (!previous && !next)) return;
      if (previous) URL.revokeObjectURL(previous.source);
      if (next) photos.set(id, next);
      else photos.delete(id);
      publish();
    }

    async function refresh() {
      if (refreshing) return;
      refreshing = true;
      try {
        for (let offset = 0; offset < partners.length && !controller.signal.aborted; offset += 4) {
          await Promise.all(partners.slice(offset, offset + 4).map(loadAvatar));
        }
      } finally {
        refreshing = false;
      }
    }

    publish();
    void refresh();
    window.addEventListener('focus', refresh);
    return () => {
      window.removeEventListener('focus', refresh);
      controller.abort();
      for (const photo of photos.values()) URL.revokeObjectURL(photo.source);
    };
  }, [partnerKey, viewerId]);

  return avatars?.viewerId === viewerId ? avatars.sources : undefined;
}
