import { API_URL, ApiError, api, json, sessionTokens } from './client';
import { Platform } from 'react-native';
import type { AccountStatus, AuthResponse, Conversation, DiscoveryPage, Interest, Match, MediaAsset, Me, Message, MessagePage, MessageRequest, Mood, NotificationPage, Premium, Presence, Profile, Report, Story } from './types';

export const authApi = {
  login: (email: string, password: string) => api<AuthResponse>('/api/auth/login', json('POST', { email, password }), false),
  register: (email: string, password: string, displayName: string, nickname?: string) => api<AuthResponse>('/api/auth/register', json('POST', { email, password, displayName, nickname: nickname || null }), false),
  me: () => api<Me>('/api/auth/me'),
  logout: (refreshToken: string) => api<void>('/api/auth/logout', json('POST', { refreshToken })),
  logoutAll: () => api<void>('/api/auth/logout-all', json('POST')),
  reactivate: (email: string, password: string) => api<AuthResponse>('/api/auth/reactivate', json('POST', { email, password }), false),
  changePassword: (currentPassword: string, newPassword: string) => api<void>('/api/auth/change-password', json('POST', { currentPassword, newPassword })),
  resetPassword: (token: string, newPassword: string) => api<void>('/api/auth/reset-password', json('POST', { token, newPassword }), false),
};
export const profileApi = {
  mine: () => api<Profile>('/api/profiles/me'),
  get: (id: string) => api<Profile>(`/api/profiles/${id}`),
  photos: (id?: string) => api<{ id: string; mediaAssetId: string; isPrimary: boolean; sortOrder?: number }[]>(id ? `/api/profiles/${id}/photos` : '/api/profiles/me/photos'),
  setPrimaryPhoto: (photoId: string) => api<void>(`/api/profiles/me/photos/${photoId}/primary`, json('PUT')),
  deletePhoto: (photoId: string) => api<void>(`/api/profiles/me/photos/${photoId}`, json('DELETE')),
  setVisibility: (visible: boolean) => api<void>(`/api/profiles/me/visibility?isVisible=${visible}`, json('PATCH')),
  update: (profile: Partial<Profile> & { displayName: string }) => api<Profile>('/api/profiles/me', json('PUT', profile)),
  privacy: () => api<Pick<Profile, 'showReadReceipts' | 'showLastSeen' | 'showOnlineStatus' | 'allowMessageRequests' | 'allowCallsFromNonMatches' | 'showPremiumBadge' | 'premiumPriorityOptIn'>>('/api/privacy'),
  updatePrivacy: (value: Record<string, boolean>) => api<void>('/api/privacy', json('PUT', value)),
  updatePreferences: (value: Record<string, boolean>) => api<void>('/api/profiles/me/preferences', json('PUT', value)),
  updateInterests: (interestIds: string[]) => api<void>('/api/interests/me', json('PUT', { interestIds }))
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
export const musicApi = {
  get: () => api<string[]>('/api/preferences/music'),
  update: (genres: string[]) => api<void>('/api/preferences/music', json('PUT', { genres }))
};
export const matchApi = {
  list: () => api<Match[]>('/api/matches'),
  requests: () => api<Match[]>('/api/matches/requests'),
  accept: (id: string) => api<Match>(`/api/matches/${id}/accept`, json('POST')),
  reject: (id: string) => api<Match>(`/api/matches/${id}/reject`, json('POST')),
  end: (id: string) => api<Match>(`/api/matches/${id}/end`, json('POST'))
};
export const chatApi = {
  conversations: () => api<Conversation[]>('/api/conversations'),
  conversation: (id: string) => api<Conversation>(`/api/conversations/${id}`),
  hideConversation: (id: string) => api<void>(`/api/conversations/${id}`, json('DELETE')),
  restoreConversation: (id: string) => api<void>(`/api/conversations/${id}/restore`, json('POST')),
  messages: (id: string) => api<MessagePage>(`/api/conversations/${id}/messages?pageSize=50`),
  profile: (id: string) => api<import('./types').Profile>(`/api/profiles/${id}`),
  send: (conversationId: string, content: string) => api<Message>('/api/messages', json('POST', { conversationId, type: 1, content })),
  sendMedia: (conversationId: string, mediaAssetId: string, type: number, content?: string) => api<Message>('/api/messages', json('POST', { conversationId, type, mediaAssetId, content: content || null })),
  deleteMessage: (id: string) => api<void>(`/api/messages/${id}/me`, json('DELETE')),
  deleteMessageForEveryone: (id: string) => api<void>(`/api/messages/${id}/everyone`, json('DELETE')),
  markDelivered: (id: string) => api<void>(`/api/messages/${id}/delivered`, json('POST')),
  markRead: (id: string) => api<void>(`/api/messages/${id}/read`, json('POST')),
  sendRequest: (recipientId: string, content: string) => api<Message>('/api/message-requests', json('POST', { recipientId, content })),
  incoming: () => api<MessageRequest[]>('/api/message-requests/incoming'),
  acceptMessageRequest: (id: string) => api<void>(`/api/message-requests/${id}/accept`, json('POST')),
  rejectMessageRequest: (id: string) => api<void>(`/api/message-requests/${id}/reject`, json('POST'))
};
export const presenceApi = { get: (userId: string) => api<Presence>(`/api/presence/${userId}`) };
export const blockApi = {
  list: () => api<string[]>('/api/blocks'),
  block: (userId: string) => api<void>(`/api/blocks/${userId}`, json('POST')),
  unblock: (userId: string) => api<void>(`/api/blocks/${userId}`, json('DELETE'))
};
export const storyApi = {
  get: (id: string) => api<Story>(`/api/stories/${id}`),
  feed: () => api<Story[]>('/api/stories/feed?take=30'),
  mine: () => api<Story[]>('/api/stories/me'),
  byUser: (userId: string) => api<Story[]>(`/api/stories/users/${userId}`),
  viewers: (id: string) => api<{ viewerId: string; viewedAtUtc: string }[]>(`/api/stories/${id}/viewers`),
  view: (id: string) => api<void>(`/api/stories/${id}/view`, json('POST')),
  create: (mediaAssetId: string, mediaType: number, caption?: string) => api<Story>('/api/stories', json('POST', { mediaAssetId, mediaType, caption })),
  delete: (id: string) => api<void>(`/api/stories/${id}`, json('DELETE')),
};
export const notificationApi = {
  list: () => api<NotificationPage>('/api/notifications?pageSize=50'),
  read: (id: string) => api<void>(`/api/notifications/${id}/read`, json('POST')),
  readAll: () => api<void>('/api/notifications/read-all', json('POST'))
};
export const mediaApi = {
  upload: async (uri: string, fileName: string, mimeType: string, category: number) => {
    // React Native's global FormData can silently omit a `file://` body on
    // some Android devices. Expo's native multipart uploader sends the file
    // URI directly, including a reliable MIME type, which ASP.NET binds to
    // IFormFile consistently. Web continues to use Blob/FormData below.
    if (Platform.OS !== 'web') {
      const accessToken = await sessionTokens.access();
      if (!accessToken) throw new ApiError('Oturum bulunamadı. Yeniden giriş yapıp tekrar dene.', 401);
      const FileSystem = await import('expo-file-system/legacy');
      let result;
      try {
        result = await FileSystem.uploadAsync(`${API_URL}/api/media?category=${category}`, uri, {
          httpMethod: 'POST',
          uploadType: FileSystem.FileSystemUploadType.MULTIPART,
          fieldName: 'file',
          mimeType: mimeType || 'image/jpeg',
          headers: { Authorization: `Bearer ${accessToken}` },
        });
      } catch (uploadError) {
        try {
          const form = new FormData();
          form.append('file', {
            uri,
            type: mimeType || 'image/jpeg',
            name: fileName || 'photo.jpg',
          } as unknown as Blob);
          return await api<MediaAsset>(`/api/media?category=${category}`, {
            method: 'POST',
            body: form,
          });
        } catch {
          throw new ApiError(uploadError instanceof Error ? uploadError.message : 'Dosya sunucuya gönderilemedi.', 0);
        }
      }
      if (result.status < 200 || result.status >= 300) {
        const payload = (() => { try { return JSON.parse(result.body || 'null'); } catch { return null; } })();
        throw new ApiError(payload?.message || payload?.detail || `Dosya yüklenemedi (${result.status}).`, result.status);
      }
      try { return JSON.parse(result.body) as MediaAsset; }
      catch { throw new ApiError('Sunucu medya yanıtını okuyamadı.', result.status); }
    }
    const form = new FormData();
    let file: Blob;
    let resolvedFileName = fileName;
    if (Platform.OS === 'web') {
      let response: Response;
      try { response = await fetch(uri, { signal: AbortSignal.timeout(20000) }); }
      catch (error) {
        if (error instanceof Error && error.name === 'TimeoutError') throw new Error('Seçilen fotoğraf 20 saniyede okunamadı. Fotoğrafı tekrar seçip dene.');
        throw new Error('Seçilen fotoğraf okunamadı. Fotoğrafı tekrar seçip dene.');
      }
      if (!response.ok) throw new Error('Seçilen medya okunamadı. Tekrar seçip dene.');
      file = await response.blob();
      // Safari can record AAC in an MP4 container even when Expo's requested
      // web preset is WebM. Keep the recorder's actual type so the API can
      // validate the bytes instead of receiving a mislabeled upload.
      const actualType = file.type.split(';', 1)[0].trim().toLowerCase();
      if (mimeType.startsWith('audio/') && ['audio/webm', 'audio/mp4', 'audio/wav', 'audio/ogg', 'audio/mpeg'].includes(actualType)) {
        mimeType = actualType;
        const extension = actualType === 'audio/webm' ? 'webm'
          : actualType === 'audio/mp4' ? 'm4a'
          : actualType === 'audio/wav' ? 'wav'
          : actualType === 'audio/ogg' ? 'ogg' : 'mp3';
        resolvedFileName = `mindmatch-voice.${extension}`;
      }
    } else throw new ApiError('Bu platformda dosya yükleme desteklenmiyor.', 400);
    form.append('file', file.slice(0, file.size, mimeType), resolvedFileName);
    return api<MediaAsset>(`/api/media?category=${category}`, { method: 'POST', body: form });
  },
  photo: (mediaAssetId: string) => api('/api/profiles/me/photos', json('POST', { mediaAssetId })),
  delete: (mediaAssetId: string) => api<void>(`/api/media/${mediaAssetId}`, json('DELETE'))
};
export const premiumApi = { mine: () => api<Premium>('/api/premium/me') };
export const reportApi = {
  submit: (reportedUserId: string, reason: number, description?: string, conversationId?: string) => api<Report>('/api/reports', json('POST', { reportedUserId, reason, description, conversationId })),
  mine: () => api<Report[]>('/api/reports/me')
};
export const callApi = {
  start: (conversationId: string, type: number) => api<import('./types').Call>('/api/calls', json('POST', { conversationId, type })),
  status: (id: string) => api<import('./types').Call>(`/api/calls/${id}`),
  answer: (id: string) => api<import('./types').Call>(`/api/calls/${id}/answer`, json('POST')),
  reject: (id: string) => api<import('./types').Call>(`/api/calls/${id}/reject`, json('POST')),
  end: (id: string) => api<import('./types').Call>(`/api/calls/${id}/end`, json('POST')),
  history: (conversationId: string) => api<import('./types').Call[]>(`/api/conversations/${conversationId}/calls`),
  credentials: () => api<{ urls: string[]; username: string; credential: string; expiresAtUtc: string }>('/api/calls/turn-credentials')
};
export const accountApi = {
  forgotPassword: (email: string) => api<void>('/api/auth/forgot-password', json('POST', { email }), false),
  activate: () => api<void>('/api/account/activate', json('POST')),
  freeze: () => api<void>('/api/account/freeze', json('POST')),
  status: () => api<AccountStatus>('/api/account/status')
};
export const adminApi = {
  reports: () => api<Report[]>('/api/admin/reports?take=50'),
  reportsByStatus: (status: number) => api<Report[]>(`/api/admin/reports?take=50&status=${status}`),
  detail: (id: string) => api<Report>(`/api/admin/reports/${id}`),
  assign: (id: string) => api<void>(`/api/admin/reports/${id}/assign`, json('POST')),
  resolve: (id: string, resolution: string, violationConfirmed: boolean) => api<void>(`/api/admin/reports/${id}/resolve`, json('POST', { resolution, violationConfirmed })),
  freeze: (userId: string, reason: string) => api<void>(`/api/admin/users/${userId}/freeze`, json('POST', { reason })),
  activate: (userId: string, reason: string) => api<void>(`/api/admin/users/${userId}/activate`, json('POST', { reason })),
  messages: (id: string) => api<Report['messages']>(`/api/admin/reports/${id}/messages`),
  history: (userId: string) => api<import('./types').ModerationHistory[]>(`/api/admin/users/${userId}/moderation-history`)
};

