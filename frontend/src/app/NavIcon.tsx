const shapes = {
  home: <><path d="m2.7 10.7 8-7.1a2 2 0 0 1 2.6 0l8 7.1M5.2 9v10a1.5 1.5 0 0 0 1.5 1.5h3.2v-5.2a2.1 2.1 0 0 1 4.2 0v5.2h3.2a1.5 1.5 0 0 0 1.5-1.5V9"/><path className="icon-tint" d="m8.8 9.2 3.2-2.8 3.2 2.8"/></>,
  compass: <><circle cx="12" cy="12" r="9.2"/><path className="icon-tint" d="m16.3 7.7-2.5 6.1-6.1 2.5 2.5-6.1Z"/><path d="m10.2 10.2 3.6 3.6"/></>,
  chat: <><path className="icon-tint" d="M21.4 11.5c0 5-4.2 8.7-9.4 8.7-1 0-2-.1-3-.4l-4.3 1.4.6-4.1a8.3 8.3 0 0 1-2.7-5.6C2.6 6.6 6.8 3 12 3s9.4 3.6 9.4 8.5Z"/><path d="m6.8 14 4-5 2.5 2.6 3.9-2.3-4.2 5.1-2.4-2.6Z" fill="currentColor" strokeWidth=".6"/></>,
  gift: <><path d="M4.4 12v7.3c0 .9.7 1.6 1.6 1.6h12c.9 0 1.6-.7 1.6-1.6V12"/><rect className="icon-tint" x="2.8" y="8" width="18.4" height="4.3" rx="1.1"/><path d="M12 8v12.5M12 8C6.5 8 5.3 6.7 5.8 4.6c.7-3.1 5.1-2.1 6.2 3.4Zm0 0c5.5 0 6.7-1.3 6.2-3.4-.7-3.1-5.1-2.1-6.2 3.4Z"/></>,
  cash: <><path d="m4.5 7.2-.6-2.1 15.8-2 .6 3"/><rect className="icon-tint" x="2.4" y="7.4" width="19.2" height="12.3" rx="2.2"/><ellipse cx="12" cy="13.5" rx="2.7" ry="3.3"/><path d="M5.6 13.5h.8m11.2 0h.8"/></>,
  profile: <><circle className="icon-tint" cx="12" cy="7.4" r="4.2"/><path className="icon-tint" d="M4.4 20.6v-1.7c0-3.5 3.3-5.7 7.6-5.7s7.6 2.2 7.6 5.7v1.7Z"/></>,
  shield: <><path className="icon-tint" d="M12 2.6c2.5 1.9 5.9 3.1 8.3 3.4v5.4c0 4.8-3.4 8.2-8.3 10.3-4.9-2.1-8.3-5.5-8.3-10.3V6c2.4-.3 5.8-1.5 8.3-3.4Z"/><path d="m8.4 11.9 2.5 2.5 4.8-5.1"/></>,
};
export function NavIcon({ name }: { name: keyof typeof shapes }) {
  return <svg className="nav-icon" viewBox="0 0 24 24" width="23" height="23" aria-hidden="true"
    fill="none" stroke="currentColor" strokeWidth="1.6" strokeLinecap="round" strokeLinejoin="round">{shapes[name]}</svg>;
}
