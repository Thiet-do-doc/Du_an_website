export const memberTabs = [
  ['/', 'Trang chủ', 'home'],
  ['/discover', 'Khám phá', 'compass'],
  ['/chat', 'Trò chuyện', 'chat'],
  ['/notifications', 'Thông báo', 'bell'],
  ['/wallet', 'Ví của tôi', 'cash'],
  ['/profile', 'Hồ sơ', 'profile'],
  ['/safety', 'An toàn', 'shield'],
] as const;

export const adminReportTab = ['/admin', 'Quản lý báo cáo', 'report'] as const;

export type NavTab = (typeof memberTabs)[number] | typeof adminReportTab;

export function getVisibleTabs(isAdmin: boolean): readonly NavTab[] {
  return isAdmin ? [...memberTabs, adminReportTab] : memberTabs;
}

export function isAppRoutePath(pathname: string): boolean {
  return memberTabs.some(([href]) => href === pathname)
    || ['/register', '/login', '/account', '/blocks', '/gifts', '/admin'].includes(pathname);
}

export function resolveRouteDestination(
  access: { userId: string; admin: boolean; complete: boolean } | null,
  sessionUserId: string | undefined,
  path: string,
  search: string,
): string | null {
  if (!access || !sessionUserId || access.userId !== sessionUserId) return null;
  if (!access.complete && (path !== '/profile' || Boolean(search)) && path !== '/account') return '/profile';
  if (path === '/gifts') return '/chat';
  if (path === '/blocks') return '/safety';
  return null;
}
