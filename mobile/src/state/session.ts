import { create } from 'zustand';
import { authApi } from '../api/mindmatch';
import { sessionTokens } from '../api/client';
import type { AuthResponse, Me } from '../api/types';

type SessionState = { user: Me | null; ready: boolean; busy: boolean; error: string | null; initialize: () => Promise<void>; signIn: (email: string, password: string) => Promise<void>; reactivate: (email: string, password: string) => Promise<void>; signUp: (email: string, password: string, name: string, nickname?: string) => Promise<void>; signOut: () => Promise<void>; setUser: (user: Me) => void };
async function saveAuth(auth: AuthResponse) { await sessionTokens.save(auth.accessToken, auth.refreshToken); return authApi.me(); }
export const useSession = create<SessionState>((set) => ({
  user: null, ready: false, busy: false, error: null,
  setUser: (user) => set({ user }),
  initialize: async () => { try { if (await sessionTokens.access()) set({ user: await authApi.me() }); } catch { await sessionTokens.clear(); } finally { set({ ready: true }); } },
  signIn: async (email, password) => { set({ busy: true, error: null }); try { set({ user: await saveAuth(await authApi.login(email, password)) }); } catch (e) { set({ error: e instanceof Error ? e.message : 'Giriş yapılamadı.' }); throw e; } finally { set({ busy: false }); } },
  reactivate: async (email, password) => { set({ busy: true, error: null }); try { set({ user: await saveAuth(await authApi.reactivate(email, password)), error: null }); } catch (e) { set({ error: e instanceof Error ? e.message : 'Hesap etkinleştirilemedi.' }); throw e; } finally { set({ busy: false }); } },
  signUp: async (email, password, name, nickname) => { set({ busy: true, error: null }); try { set({ user: await saveAuth(await authApi.register(email, password, name, nickname)) }); } catch (e) { set({ error: e instanceof Error ? e.message : 'Kayıt oluşturulamadı.' }); throw e; } finally { set({ busy: false }); } },
  signOut: async () => { const refresh = await sessionTokens.refresh(); try { if (refresh) await authApi.logout(refresh); } catch {} await sessionTokens.clear(); set({ user: null, error: null }); }
}));

