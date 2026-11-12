import { NavIcon } from '../../app/NavIcon';

export function AccountAvatar({ source }: { source?: string }) {
  return <span className="account-icon" aria-hidden="true">
    {source ? <img src={source} alt="" /> : <NavIcon name="profile" />}
  </span>;
}
