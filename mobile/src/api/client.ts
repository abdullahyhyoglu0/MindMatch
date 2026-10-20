import * as SecureStore from 'expo-secure-store';
import { Platform } from 'react-native';

export const API_URL = (process.env.EXPO_PUBLIC_API_URL || 'http://localhost:5193').replace(/\/$/, '');
const ACCESS_KEY = 'mindmatch.access';
const REFRESH_KEY = 'mindmatch.refresh';
let refreshTask: Promise<string | null> | null = null;

export class ApiError extends Error {
  constructor(message: string, readonly status: number) { super(message); this.name = 'ApiError'; }
}

const tokenStorage = {
  get(key: string) {
    if (Platform.OS === 'web') return Promise.resolve(typeof localStorage === 'undefined' ? null : localStorage.getItem(key));
    return SecureStore.getItemAsync(key);
  },
  set(key: string, value: string) {
    if (Platform.OS === 'web') { if (typeof localStorage !== 'undefined') localStorage.setItem(key, value); return Promise.resolve(); }
    return SecureStore.setItemAsync(key, value);
  },
  delete(key: string) {
    if (Platform.OS === 'web') { if (typeof localStorage !== 'undefined') localStorage.removeItem(key); return Promise.resolve(); }
    return SecureStore.deleteItemAsync(key);
  },
};

export const sessionTokens = {
  async access() { return tokenStorage.get(ACCESS_KEY); },
  async refresh() { return tokenStorage.get(REFRESH_KEY); },
  async save(access: string, refresh: string) { await Promise.all([tokenStorage.set(ACCESS_KEY, access), tokenStorage.set(REFRESH_KEY, refresh)]); },
  async clear() { await Promise.all([tokenStorage.delete(ACCESS_KEY), tokenStorage.delete(REFRESH_KEY)]); }
};

async function rotateToken() {
  if (!refreshTask) refreshTask = (async () => {
    const refreshToken = await sessionTokens.refresh();
    if (!refreshToken) return null;
    try {
      const response = await fetch(`${API_URL}/api/auth/refresh`, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ refreshToken }), signal: AbortSignal.timeout(15000) });
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
  const isFileUpload = options.body instanceof FormData;
  try { response = await fetch(`${API_URL}${path}`, { ...options, headers, signal: options.signal || AbortSignal.timeout(isFileUpload ? 120000 : 20000) }); }
  catch (error) { throw new ApiError(error instanceof Error && error.name === 'TimeoutError' ? 'Sunucu zaman aşımına uğradı.' : 'Sunucuya bağlanılamadı. API adresini ve tüneli kontrol et.', 0); }
  if (response.status === 401 && allowRefresh && access) {
    const next = await rotateToken();
    if (next) return api<T>(path, options, false);
    await sessionTokens.clear();
    throw new ApiError('Oturum yenilenemedi. Yeniden giriş yapıp tekrar dene.', 401);
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

