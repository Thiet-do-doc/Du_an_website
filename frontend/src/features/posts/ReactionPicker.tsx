import { useEffect, useId, useRef, useState } from 'react';

export type Reaction = 'like' | 'haha' | 'angry' | 'sad';
const choices: [Reaction, string, string][] = [['like', '👍', 'Thích'], ['haha', '😆', 'Haha'], ['angry', '😠', 'Tức giận'], ['sad', '😢', 'Buồn']];

export function ReactionPicker({ value, counts, disabled, onSelect }: {
  value: Reaction | null; counts: Record<Reaction, number>; disabled: boolean; onSelect: (kind: Reaction) => void;
}) {
  const [open, setOpen] = useState(false);
  const root = useRef<HTMLDivElement>(null);
  const trigger = useRef<HTMLButtonElement>(null);
  const timer = useRef<number | null>(null);
  const held = useRef(false);
  const origin = useRef<{ x: number; y: number } | null>(null);
  const dialogId = useId();
  const selected = choices.find(([kind]) => kind === value) ?? choices[0];
  const total = Object.values(counts).reduce((sum, count) => sum + count, 0);
  function cancelHold() {
    if (timer.current !== null) window.clearTimeout(timer.current);
    timer.current = null; origin.current = null;
  }
  useEffect(() => () => { if (timer.current !== null) window.clearTimeout(timer.current); }, []);
  useEffect(() => {
    if (!open) return;
    const outside = (event: PointerEvent) => { if (!root.current?.contains(event.target as Node)) setOpen(false); };
    const escape = (event: KeyboardEvent) => {
      if (event.key === 'Escape') { setOpen(false); trigger.current?.focus(); }
    };
    document.addEventListener('pointerdown', outside);
    document.addEventListener('keydown', escape);
    return () => { document.removeEventListener('pointerdown', outside); document.removeEventListener('keydown', escape); };
  }, [open]);
  return <div className="reaction-picker" ref={root} onBlur={event => {
    if (!event.currentTarget.contains(event.relatedTarget as Node)) { cancelHold(); setOpen(false); }
  }}>
    <button ref={trigger} type="button" className="reaction-trigger" disabled={disabled} aria-pressed={value !== null}
      aria-haspopup="dialog" aria-expanded={open} aria-controls={open ? dialogId : undefined}
      aria-label={selected[2] + ' · Nhấn giữ để chọn cảm xúc'} title="Bấm để thích hoặc gỡ cảm xúc. Nhấn giữ để chọn cảm xúc khác."
      onPointerDown={event => {
        if (event.button !== 0 || disabled) return;
        cancelHold(); held.current = false; origin.current = { x: event.clientX, y: event.clientY };
        timer.current = window.setTimeout(() => { timer.current = null; held.current = true; setOpen(true); }, 450);
      }}
      onPointerMove={event => {
        if (origin.current && Math.hypot(event.clientX - origin.current.x, event.clientY - origin.current.y) > 10) cancelHold();
      }}
      onPointerUp={cancelHold} onPointerCancel={cancelHold} onPointerLeave={cancelHold}
      onContextMenu={event => { event.preventDefault(); cancelHold(); held.current = true; setOpen(true); }}
      onKeyDown={event => {
        if (['ArrowDown', 'ArrowUp'].includes(event.key) || event.key === 'F10' && event.shiftKey) {
          event.preventDefault(); setOpen(true);
          requestAnimationFrame(() => root.current?.querySelector<HTMLButtonElement>('.reaction-options button')?.focus());
        }
      }}
      onClick={() => {
        if (held.current) { held.current = false; return; }
        if (open) { setOpen(false); return; }
        onSelect(value ?? 'like');
      }}><span aria-hidden="true">{selected[1]}</span> {selected[2]}</button>
    {total > 0 && <span className="reaction-summary" aria-label={choices.filter(([kind]) => counts[kind] > 0).map(([kind, , label]) => label + ': ' + counts[kind]).join(', ')}>
      <span aria-hidden="true">{choices.filter(([kind]) => counts[kind] > 0).map(([, emoji]) => emoji).join('')} {total}</span>
    </span>}
    {open && <div id={dialogId} className="reaction-options" role="dialog" aria-label="Chọn cảm xúc">
      {choices.map(([kind, emoji, label]) => <button type="button" key={kind} disabled={disabled} aria-pressed={value === kind}
        aria-label={label} title={label} onClick={() => { held.current = false; setOpen(false); onSelect(kind); trigger.current?.focus(); }}>
        <span aria-hidden="true">{emoji}</span><small>{label}</small>
      </button>)}
    </div>}
  </div>;
}
