import { useEffect, useState, type HTMLAttributes, type ImgHTMLAttributes } from 'react';
import { authenticatedFetch } from '../auth/api';

function usePhoto(url?: string | null) {
  const [source, setSource] = useState<string>();
  useEffect(() => {
    let disposed = false; let objectUrl: string | undefined;
    setSource(undefined);
    if (url) void authenticatedFetch<Blob>(url, {}, r => r.blob()).then(blob => {
      if (!disposed) { objectUrl = URL.createObjectURL(blob); setSource(objectUrl); }
    }).catch(() => { /* Keep the placeholder when access is revoked or the image is unavailable. */ });
    return () => { disposed = true; if (objectUrl) URL.revokeObjectURL(objectUrl); };
  }, [url]);
  return source;
}
export function PrivatePhoto({ src, alt, ...props }: ImgHTMLAttributes<HTMLImageElement>) {
  const source = usePhoto(src);
  return source ? <img {...props} src={source} alt={alt} /> : <span className="photo-placeholder" role="img" aria-label={alt}>♥</span>;
}
export function PhotoBackground({ photoUrl, style, ...props }: HTMLAttributes<HTMLDivElement> & { photoUrl?: string | null }) {
  const source = usePhoto(photoUrl);
  return <div {...props} style={{ ...style, backgroundImage: source ? `url("${source}")` : undefined }} />;
}
