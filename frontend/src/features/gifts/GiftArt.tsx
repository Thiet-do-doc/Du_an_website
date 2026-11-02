const illustrations = {
  tulip: <g transform="rotate(12 50 50)">
    <path d="M48 84c7-15 7-29 5-42" fill="none" stroke="#58795d" strokeWidth="3"/>
    <path d="M52 72Q30 69 28 50q20 4 24 22Zm2-13q1-15 17-21-1 16-17 21Z" fill="#95b89a" stroke="#597b62"/>
    <path d="M34 23q8 1 12 8l6-15q7 7 9 16l9-9q2 26-15 29-17 1-21-29Z" fill="#ed9eb5" stroke="#994866"/>
    <path d="M34 23q17 12 21 29m15-29Q52 36 52 49" fill="none" stroke="#bf6485"/>
  </g>,
  letter: <g transform="rotate(-10 50 50)">
    <path d="m16 43 32-24q2-2 5 0l31 24v36H16Z" fill="#d8a69f" stroke="#92625e"/>
    <rect x="26" y="25" width="48" height="46" rx="3" fill="#fffcf3" stroke="#b38a78"/>
    <path d="M35 37h17m-17 7h26m-26 7h17" stroke="#cbb5a4"/>
    <path d="m16 43 34 20 34-20v36H16Z" fill="#f5c4c1" stroke="#a56971"/>
    <path d="m17 78 24-19m42 19L60 60" fill="none" stroke="#c48e92"/>
    <path d="M50 67c-17-10-7-17 0-10 7-7 17 0 0 10Z" fill="#b95c73" stroke="none"/>
  </g>,
  bear: <g fill="#c99b80" stroke="#825c53">
    <circle cx="32" cy="28" r="10"/><circle cx="68" cy="28" r="10"/>
    <ellipse cx="50" cy="68" rx="20" ry="17"/><ellipse cx="50" cy="41" rx="25" ry="21"/>
    <ellipse cx="50" cy="48" rx="12" ry="8" fill="#f3d6b9" stroke="none"/>
    <g fill="#573f40" stroke="none"><circle cx="40" cy="39" r="2"/><circle cx="60" cy="39" r="2"/><path d="M46 44q4-3 8 0l-4 4Z"/></g>
    <path d="M50 81c-28-16-12-30 0-18 12-12 28 2 0 18Z" fill="#e6a0b0" stroke="#ad697b"/>
    <ellipse cx="31" cy="65" rx="8" ry="6" transform="rotate(30 31 65)"/><ellipse cx="69" cy="65" rx="8" ry="6" transform="rotate(-30 69 65)"/>
  </g>,
};
export function GiftArt({ code }: { code: string }) {
  return <svg className={'gift-art art-' + code} viewBox="0 0 100 100" aria-hidden="true"
    strokeWidth="1.7" strokeLinecap="round" strokeLinejoin="round">
    <circle cx="50" cy="50" r="40" fill="currentColor" opacity=".07"/>
    {illustrations[code as keyof typeof illustrations]}
  </svg>;
}
