import { api, json } from './client';
import { File } from 'expo-file-system';
import type { AuthResponse, Conversation, DiscoveryPage, Interest, Match, MediaAsset, Me, Message, MessagePage, Mood, NotificationPage, Premium, Profile, Report, Story } from './types';

export const authApi = {
  login: (email: string, password: string) => api<AuthResponse>('/api/auth/login', json('POST', { email, password }), false),
  register: (email: string, password: string, displayName: string, nickname?: string) => api<AuthResponse>('/api/auth/register', json('POST', { email, password, displayName, nickname: nickname || null }), false),
  me: () => api<Me>('/api/auth/me'),
  logout: (refreshToken: string) => api<void>('/api/auth/logout', json('POST', { refreshToken }))
};
export const profileApi = {
  mine: () => api<Profile>('/api/profiles/me'),
  get: (id: string) => api<Profile>(`/api/profiles/${id}`),
  photos: (id?: string) => api<{ id: string; mediaAssetId: string; isPrimary: boolean }[]>(id ? `/api/profiles/${id}/photos` : '/api/profiles/me/photos'),
  setVisibility: (visible: boolean) => api<void>(`/api/profiles/me/visibility?isVisible=${visible}`, json('PATCH')),
  update: (profile: Partial<Profile> & { displayName: string }) => api<Profile>('/api/profiles/me', json('PUT', profile)),
  updatePrivacy: (value: Record<string, boolean>) => api<void>('/api/privacy', json('PUT', value)),
  updatePreferences: (value: Record<string, boolean>) => api<void>('/api/profiles/me/preferences', json('PUT', value))
};
export const discoveryApi = {
  list: (page = 1, pageSize = 20, city?: string) => api<DiscoveryPage>(`/api/discovery?page=${page}&pageSize=${pageSize}${city ? `&city=${encodeURIComponent(city)}` : ''}`),
  seen: (id: string) => api<void>(`/api/discovery/${id}/seen`, json('POST')),
  skip: (id: string) => api<void>(`/api/discovery/${id}/reject`, json('POST')),
  match: (receiverId: string) => api<Match>('/api/matches', json('POST', { receiverId })),
  mood: () => api<Mood>('/api/moodcheckins/me/latest'),
  checkIn: (mood: number, energy: number, desiredConversation: number) => api<Mood>('/api/moodcheckins', json('POST', { mood, energy, desiredConversation }))
};
export const interestApi = { list: () => api<Interest[]>('/api/interests') };
export const matchApi = {
  list: () => api<Match[]>('/api/matches'),
  requests: () => api<Match[]>('/api/matches/requests'),
  accept: (id: string) => api<Match>(`/api/matches/${id}/accept`, json('POST')),
  reject: (id: string) => api<Match>(`/api/matches/${id}/reject`, json('POST'))
};
export const chatApi = {
  conversations: () => api<Conversation[]>('/api/conversations'),
  messages: (id: string) => api<MessagePage>(`/api/conversations/${id}/messages?pageSize=50`),
  profile: (id: string) => api<import('./types').Profile>(`/api/profiles/${id}`),
  send: (conversationId: string, content: string) => api<Message>('/api/messages', json('POST', { conversationId, type: 1, content })),
  sendMedia: (conversationId: string, mediaAssetId: string, type: number) => api<Message>('/api/messages', json('POST', { conversationId, type, mediaAssetId })),
  deleteMessage: (id: string) => api<void>(`/api/messages/${id}/me`, json('DELETE')),
  incoming: () => api<any[]>('/api/message-requests/incoming'),
  acceptMessageRequest: (id: string) => api<void>(`/api/message-requests/${id}/accept`, json('POST')),
  rejectMessageRequest: (id: string) => api<void>(`/api/message-requests/${id}/reject`, json('POST'))
};
export const storyApi = {
  get: (id: string) => api<Story>(`/api/stories/${id}`),
  feed: () => api<Story[]>('/api/stories/feed?take=30'),
  mine: () => api<Story[]>('/api/stories/me'),
  view: (id: string) => api<void>(`/api/stories/${id}/view`, json('POST')),
  create: (mediaAssetId: string, mediaType: number, caption?: string) => api<Story>('/api/stories', json('POST', { mediaAssetId, mediaType, caption }))
};
export const notificationApi = {
  list: () => api<NotificationPage>('/api/notifications?pageSize=50'),
  read: (id: string) => api<void>(`/api/notifications/${id}/read`, json('POST')),
  readAll: () => api<void>('/api/notifications/read-all', json('POST'))
};
export const mediaApi = {
  upload: async (uri: string, fileName: string, mimeType: string, category: number) => {
    const form = new FormData();
    const file = new File(uri);
    form.append('file', file.slice(0, file.size, mimeType), fileName);
    return api<MediaAsset>(`/api/media?category=${category}`, { method: 'POST', body: form });
  },
  photo: (mediaAssetId: string) => api('/api/profiles/me/photos', json('POST', { mediaAssetId })),
  delete: (mediaAssetId: string) => api<void>(`/api/media/${mediaAssetId}`, json('DELETE'))
};
export const premiumApi = { mine: () => api<Premium>('/api/premium/me') };
export const reportApi = {
  submit: (reportedUserId: string, reason: number, description?: string, conversationId?: string) => api<Report>('/api/reports', json('POST', { reportedUserId, reason, description, conversationId }))
};
export const callApi = {
  start: (conversationId: string, type: number) => api<import('./types').Call>('/api/calls', json('POST', { conversationId, type })),
  status: (id: string) => api<import('./types').Call>(`/api/calls/${id}`),
  answer: (id: string) => api<import('./types').Call>(`/api/calls/${id}/answer`, json('POST')),
  reject: (id: string) => api<import('./types').Call>(`/api/calls/${id}/reject`, json('POST')),
  end: (id: string) => api<import('./types').Call>(`/api/calls/${id}/end`, json('POST')),
  credentials: () => api<{ urls: string[]; username: string; credential: string; expiresAtUtc: string }>('/api/calls/turn-credentials')
};
export const accountApi = {
  forgotPassword: (email: string) => api<void>('/api/auth/forgot-password', json('POST', { email }), false),
  activate: () => api<void>('/api/account/activate', json('POST')),
  freeze: () => api<void>('/api/account/freeze', json('POST'))
};
export const adminApi = {
  reports: () => api<Report[]>('/api/admin/reports?take=50'),
  detail: (id: string) => api<Report>(`/api/admin/reports/${id}`),
  assign: (id: string) => api<void>(`/api/admin/reports/${id}/assign`, json('POST')),
  resolve: (id: string, resolution: string, violationConfirmed: boolean) => api<void>(`/api/admin/reports/${id}/resolve`, json('POST', { resolution, violationConfirmed })),
  freeze: (userId: string, reason: string) => api<void>(`/api/admin/users/${userId}/freeze`, json('POST', { reason })),
  activate: (userId: string, reason: string) => api<void>(`/api/admin/users/${userId}/activate`, json('POST', { reason }))
};

