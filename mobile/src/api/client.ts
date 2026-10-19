import * as SecureStore from 'expo-secure-store';

export const API_URL = (process.env.EXPO_PUBLIC_API_URL || 'http://localhost:5193').replace(/\/$/, '');
const ACCESS_KEY = 'mindmatch.access';
const REFRESH_KEY = 'mindmatch.refresh';
let refreshTask: Promise<string | null> | null = null;

export class ApiError extends Error {
  constructor(message: string, readonly status: number) { super(message); this.name = 'ApiError'; }
}

export const sessionTokens = {
  async access() { return SecureStore.getItemAsync(ACCESS_KEY); },
  async refresh() { return SecureStore.getItemAsync(REFRESH_KEY); },
  async save(access: string, refresh: string) { await Promise.all([SecureStore.setItemAsync(ACCESS_KEY, access), SecureStore.setItemAsync(REFRESH_KEY, refresh)]); },
  async clear() { await Promise.all([SecureStore.deleteItemAsync(ACCESS_KEY), SecureStore.deleteItemAsync(REFRESH_KEY)]); }
};

async function rotateToken() {
  if (!refreshTask) refreshTask = (async () => {
    const refreshToken = await sessionTokens.refresh();
    if (!refreshToken) return null;
    try {
      const response = await fetch(`${API_URL}/api/auth/refresh`, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ refreshToken }) });
      if (!response.ok) { await sessionTokens.clear(); return null; }
      const auth = await response.json();
      await sessionTokens.save(auth.accessToken, auth.refreshToken);
      return auth.accessToken as string;
    } catch { return null; }
  })().finally(() => { refreshTask = null; });
  return refreshTask;
}

export async function api<T>(path: string, options: RequestInit = {}, allowRefresh = true): Promise<T> {
  const headers = new Headers(options.headers || {});
  const access = await sessionTokens.access();
  if (access) headers.set('Authorization', `Bearer ${access}`);
  if (options.body && !(options.body instanceof FormData)) headers.set('Content-Type', 'application/json');
  let response: Response;
  try { response = await fetch(`${API_URL}${path}`, { ...options, headers, signal: options.signal || AbortSignal.timeout(20000) }); }
  catch (error) { throw new ApiError(error instanceof Error && error.name === 'TimeoutError' ? 'Sunucu zaman aşımına uğradı.' : 'Sunucuya bağlanılamadı. API adresini ve tüneli kontrol et.', 0); }
  if (response.status === 401 && allowRefresh && access) {
    const next = await rotateToken();
    if (next) return api<T>(path, options, false);
    await sessionTokens.clear();
  }
  if (!response.ok) {
    const body = await response.json().catch(() => null);
    const message = body?.message || body?.detail || body?.title || `İstek başarısız (${response.status})`;
    throw new ApiError(message, response.status);
  }
  if (response.status === 204) return undefined as T;
  return response.json() as Promise<T>;
}

export const json = (method: string, body?: unknown): RequestInit => ({ method, ...(body === undefined ? {} : { body: JSON.stringify(body) }) });

