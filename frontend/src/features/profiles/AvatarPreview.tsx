import { useId, useRef, type ReactNode } from 'react';

export function AvatarPreview({ source, name, children }: { source: string; name?: string; children: ReactNode }) {
  const dialog = useRef<HTMLDialogElement>(null);
  const titleId = useId();

  return <>
    <button type="button" className="avatar-preview-trigger" aria-label="Xem ảnh đại diện"
      aria-haspopup="dialog" onClick={() => dialog.current?.showModal()}>
      {children}
    </button>
    <dialog ref={dialog} className="avatar-preview-dialog" aria-labelledby={titleId}>
      <header>
        <h2 id={titleId}>Ảnh đại diện</h2>
        <form method="dialog"><button className="btn btn-outline-dark">Đóng</button></form>
      </header>
      <img src={source} alt={name ? `Ảnh đại diện của ${name}` : 'Ảnh đại diện của bạn'} />
    </dialog>
  </>;
}
