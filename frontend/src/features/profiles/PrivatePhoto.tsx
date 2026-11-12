import { useEffect, useState, type HTMLAttributes, type ImgHTMLAttributes } from 'react';
import { getPrivatePhoto } from './api';
import { AvatarPreview } from './AvatarPreview';

export function usePrivatePhoto(url?: string | null) {
  const [photo, setPhoto] = useState<{ url: string; source: string }>();
  useEffect(() => {
    let disposed = false; let objectUrl: string | undefined;
    setPhoto(undefined);
    if (url) void getPrivatePhoto(url).then(blob => {
      if (!disposed) {
        objectUrl = URL.createObjectURL(blob);
        setPhoto({ url, source: objectUrl });
      }
    }).catch(() => { /* Keep the placeholder when access is revoked or the image is unavailable. */ });
    return () => { disposed = true; if (objectUrl) URL.revokeObjectURL(objectUrl); };
  }, [url]);
  return photo?.url === url ? photo?.source : undefined;
}
export function PrivatePhoto({ src, alt, previewAvatar, ...props }: ImgHTMLAttributes<HTMLImageElement> & { previewAvatar?: boolean }) {
  const source = usePrivatePhoto(src);
  if (!source) return <span className="photo-placeholder" role="img" aria-label={alt}>♥</span>;
  const image = <img {...props} src={source} alt={alt} />;
  return previewAvatar ? <AvatarPreview source={source}>{image}</AvatarPreview> : image;
}
export function PhotoBackground({ photoUrl, style, ...props }: HTMLAttributes<HTMLDivElement> & { photoUrl?: string | null }) {
  const source = usePrivatePhoto(photoUrl);
  return <div {...props} style={{ ...style, backgroundImage: source ? `url("${source}")` : undefined }} />;
}
