export type Session = { token: string; expiresAt: string; user: { id: string; email: string } };
export type ApiProblem = { title?: string; detail?: string; errors?: Record<string, string[]> };
export class ApiError extends Error {
  constructor(public status: number, public problem: ApiProblem) { super(problem.detail || problem.title || `HTTP ${status}`); }
}
let accessToken: string | null = null;
let refreshTask: Promise<Session | null> | null = null;
const listeners = new Set<(session: Session | null) => void>();
export function subscribe(listener: (session: Session | null) => void) { listeners.add(listener); return () => { listeners.delete(listener); }; }
export function publish(session: Session | null) { accessToken = session?.token ?? null; listeners.forEach(listener => listener(session)); }
async function parse<T>(response: Response): Promise<T> {
  const body = response.status === 204 ? null : await response.json();
  if (!response.ok) throw new ApiError(response.status, body ?? {});
  return body as T;
}
async function csrf(): Promise<string> {
  const response = await fetch('/api/v1/auth/csrf', { credentials: 'same-origin', cache: 'no-store', signal: AbortSignal.timeout(15000) });
  return (await parse<{ token: string }>(response)).token;
}
export async function publicPost<T>(path: string, body: unknown = {}): Promise<T> {
  const headers: Record<string, string> = { 'Content-Type': 'application/json' };
  if (['login', 'refresh', 'logout'].includes(path)) headers['X-CSRF-TOKEN'] = await csrf();
  return parse<T>(await fetch(`/api/v1/auth/${path}`, { method: 'POST', credentials: 'same-origin',
    headers, body: JSON.stringify(body), cache: 'no-store', signal: AbortSignal.timeout(15000) }));
}
// Một lần refresh trong mỗi tab; Web Locks tuần tự hóa các tab cùng origin.
async function withSessionLock<T>(action: () => Promise<T>): Promise<T> {
  return navigator.locks ? navigator.locks.request('datingapp-session', action) : action();
}
export async function login(email: string, password: string): Promise<Session> {
  const session = await withSessionLock(() => publicPost<Session>('login', { email, password }));
  publish(session); return session;
}
export function refresh(): Promise<Session | null> {
  if (refreshTask) return refreshTask;
  refreshTask = withSessionLock(async () => {
    try { const session = await publicPost<Session>('refresh'); publish(session); return session; }
    catch (error) { publish(null); if (error instanceof ApiError && error.status === 401) return null; throw error; }
  }).finally(() => { refreshTask = null; });
  return refreshTask;
}
export async function logout() {
  await withSessionLock(() => publicPost('logout'));
  publish(null);
  const channel = new BroadcastChannel('datingapp-auth'); channel.postMessage('logout'); channel.close();
}
export async function authenticatedFetch<T>(path: string, init: RequestInit = {}, readBody: (response: Response) => Promise<T> = parse<T>): Promise<T> {
  async function send() {
    const headers = new Headers(init.headers);
    if (accessToken) headers.set('Authorization', `Bearer ${accessToken}`);
    return fetch(path, { ...init, headers, credentials: 'same-origin', cache: 'no-store', signal: init.signal ?? AbortSignal.timeout(15000) });
  }
  let response = await send();
  if (response.status === 401 && await refresh()) response = await send();
  if (response.status === 401) publish(null);
  if (!response.ok) return parse<T>(response);
  return readBody(response);
}
