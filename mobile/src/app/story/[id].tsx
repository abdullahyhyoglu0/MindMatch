import { useEffect, useState } from 'react';
import { Platform, Pressable, ScrollView, StyleSheet, Text, View } from 'react-native';
import { router, useLocalSearchParams } from 'expo-router';
import { useQueries, useQuery } from '@tanstack/react-query';
import { useVideoPlayer, VideoView } from 'expo-video';
import { Eye, X } from 'lucide-react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';
import { API_URL, sessionTokens } from '../../api/client';
import { profileApi, storyApi } from '../../api/mindmatch';
import { Avatar, Loading, ProtectedImage } from '../../components/ui';
import { useSession } from '../../state/session';
import { colors } from '../../theme/tokens';

export default function StoryScreen() {
  const { id } = useLocalSearchParams<{ id: string }>();
  const insets = useSafeAreaInsets();
  const user = useSession(state => state.user);
  const [token, setToken] = useState<string | null>(null);
  const [videoFailure, setVideoFailure] = useState<{ storyId: string; message: string } | null>(null);
  const videoError = videoFailure?.storyId === id ? videoFailure.message : null;
  const story = useQuery({ queryKey: ['story', id], queryFn: () => storyApi.get(id), enabled: Boolean(id) });
  const ownStory = story.data?.userId === user?.userId;
  const owner = useQuery({ queryKey: ['profile', story.data?.userId], queryFn: () => profileApi.get(story.data!.userId), enabled: Boolean(story.data?.userId) });
  const viewers = useQuery({ queryKey: ['story-viewers', id], queryFn: () => storyApi.viewers(id), enabled: Boolean(id && ownStory), refetchInterval: ownStory ? 15000 : false });
  const viewerProfiles = useQueries({ queries: (viewers.data ?? []).slice(0, 20).map(viewer => ({ queryKey: ['profile', viewer.viewerId], queryFn: () => profileApi.get(viewer.viewerId) })) });
  const player = useVideoPlayer(null);

  function closeStory() {
    // The viewer can be opened from a deep link, where the browser history is
    // not the Expo Router stack. Always return to the story feed explicitly.
    router.replace('/(tabs)/stories');
  }

  useEffect(() => { sessionTokens.access().then(setToken); }, []);
  useEffect(() => {
    if (!story.data || ownStory) return;
    storyApi.view(id).catch(() => {});
  }, [id, ownStory, story.data]);
  useEffect(() => {
    const mediaId = story.data?.mediaAssetId;
    if (story.data?.mediaType !== 2 || !mediaId || !token) return;
    let cancelled = false;
    let objectUrl: string | null = null;
    if (Platform.OS === 'web') {
      const controller = new AbortController();
      fetch(`${API_URL}/api/media/${mediaId}`, { headers: { Authorization: `Bearer ${token}` }, signal: controller.signal })
        .then(async response => {
          if (!response.ok) throw new Error(`Video alınamadı (${response.status}).`);
          return response.blob();
        })
        .then(async blob => {
          if (cancelled) return;
          objectUrl = URL.createObjectURL(blob);
          player.muted = true;
          await player.replaceAsync(objectUrl);
          if (!cancelled) player.play();
        })
        .catch(error => { if (!cancelled) setVideoFailure({ storyId: id, message: error instanceof Error ? error.message : 'Video oynatılamadı.' }); });
      return () => { cancelled = true; controller.abort(); player.pause(); if (objectUrl) URL.revokeObjectURL(objectUrl); };
    }
    player.replaceAsync({ uri: `${API_URL}/api/media/${mediaId}`, headers: { Authorization: `Bearer ${token}` } }).then(() => player.play()).catch(error => setVideoFailure({ storyId: id, message: error instanceof Error ? error.message : 'Video oynatılamadı.' }));
    return () => { cancelled = true; player.pause(); };
  }, [id, story.data?.mediaAssetId, story.data?.mediaType, token, player]);

  if (story.isLoading) return <View style={s.loading}><Loading label="Hikâye açılıyor" /></View>;
  const isVideo = story.data?.mediaType === 2;
  return (
    <View style={s.root}>
      {isVideo && story.data?.mediaAssetId ? <VideoView player={player} style={StyleSheet.absoluteFill} contentFit="contain" nativeControls /> : story.data?.mediaAssetId ? <ProtectedImage mediaAssetId={story.data.mediaAssetId} style={StyleSheet.absoluteFill} contentFit="contain" /> : <View style={s.blank} />}
      <View style={s.scrim} pointerEvents="none" />
      {videoError ? <View style={s.videoError}><Text style={s.videoErrorTitle}>Video açılamadı</Text><Text style={s.videoErrorText}>{videoError}</Text></View> : null}
      <View style={[s.progressTrack, { top: insets.top + 8 }]}><View style={s.progressFill} /></View>
      <Pressable accessibilityRole="button" accessibilityLabel="Hikâyeyi kapat" onPress={closeStory} style={[s.close, { top: insets.top + 24 }]}><X size={21} color={colors.white} /></Pressable>
      <View style={[s.top, { top: insets.top + 27 }]}><Avatar name={owner.data?.displayName || 'MindMatch'} size={42} /><View><Text style={s.user}>{owner.data?.displayName || 'MindMatch kullanıcısı'}</Text><Text style={s.date}>{story.data ? new Date(story.data.createdAtUtc).toLocaleString('tr-TR', { day: 'numeric', month: 'short', hour: '2-digit', minute: '2-digit' }) : ''}</Text></View></View>
      {story.data?.caption ? <Text style={[s.caption, { bottom: ownStory ? insets.bottom + 158 : insets.bottom + 28 }]}>{story.data.caption}</Text> : null}
      {ownStory ? <View style={[s.viewerPanel, { paddingBottom: Math.max(insets.bottom, 16) }]}>
        <View style={s.viewerHeading}><View style={s.viewerTitle}><Eye size={17} color={colors.white} /><Text style={s.viewerCount}>{viewers.data?.length ?? 0} görüntüleme</Text></View><Text style={s.viewerHint}>{viewers.isFetching ? 'Güncelleniyor…' : 'Hikâyeni görenler'}</Text></View>
        {viewers.data?.length ? <ScrollView horizontal showsHorizontalScrollIndicator={false} contentContainerStyle={s.viewerList}>{viewers.data.slice(0, 20).map((viewer, index) => {
          const profile = viewerProfiles[index]?.data;
          return <View key={viewer.viewerId} style={s.viewerPerson}><View style={s.viewerAvatar}><Avatar name={profile?.displayName ?? 'Üye'} size={43} /></View><Text numberOfLines={1} style={s.viewerName}>{profile?.displayName ?? 'Görüntüleyen'}</Text><Text style={s.viewerTime}>{new Date(viewer.viewedAtUtc).toLocaleTimeString('tr-TR', { hour: '2-digit', minute: '2-digit' })}</Text></View>;
        })}</ScrollView> : <Text style={s.emptyViewers}>Henüz görüntüleyen yok. İzlenmeler burada görünecek.</Text>}
      </View> : null}
    </View>
  );
}

const s = StyleSheet.create({
  root: { flex: 1, backgroundColor: '#111018', justifyContent: 'center' }, loading: { flex: 1, backgroundColor: colors.canvas }, blank: { ...StyleSheet.absoluteFill, backgroundColor: '#373044' }, scrim: { ...StyleSheet.absoluteFill, backgroundColor: '#00000012' },
  progressTrack: { position: 'absolute', left: 16, right: 16, height: 3, borderRadius: 2, backgroundColor: '#FFFFFF70', zIndex: 3, overflow: 'hidden' }, progressFill: { height: '100%', width: '100%', backgroundColor: colors.white },
  close: { position: 'absolute', zIndex: 4, right: 16, width: 42, height: 42, borderRadius: 15, alignItems: 'center', justifyContent: 'center', backgroundColor: '#17132999' }, top: { position: 'absolute', zIndex: 3, left: 17, flexDirection: 'row', alignItems: 'center', gap: 10 }, user: { fontSize: 13, color: colors.white, fontWeight: '800' }, date: { fontSize: 10, color: '#FFFFFFC0', marginTop: 4 },
  caption: { position: 'absolute', alignSelf: 'center', fontSize: 16, color: colors.white, fontWeight: '700', textShadowColor: '#000', textShadowRadius: 5, paddingHorizontal: 22, textAlign: 'center' },
  videoError: { position: 'absolute', zIndex: 2, alignSelf: 'center', maxWidth: '85%', padding: 16, borderRadius: 18, backgroundColor: '#171329E8', alignItems: 'center' }, videoErrorTitle: { color: colors.white, fontSize: 14, fontWeight: '900' }, videoErrorText: { color: '#E1DCEC', fontSize: 11, textAlign: 'center', marginTop: 5 },
  viewerPanel: { position: 'absolute', left: 0, right: 0, bottom: 0, paddingTop: 16, paddingHorizontal: 18, backgroundColor: '#171329E8', borderTopLeftRadius: 24, borderTopRightRadius: 24 }, viewerHeading: { flexDirection: 'row', justifyContent: 'space-between', alignItems: 'center', marginBottom: 13 }, viewerTitle: { flexDirection: 'row', alignItems: 'center', gap: 8 }, viewerCount: { fontSize: 14, color: colors.white, fontWeight: '900' }, viewerHint: { fontSize: 10, color: '#D8D2F1' }, viewerList: { gap: 15, paddingBottom: 4 }, viewerPerson: { width: 58, alignItems: 'center' }, viewerAvatar: { borderRadius: 25, borderWidth: 2, borderColor: colors.coral, padding: 2 }, viewerName: { fontSize: 9, color: colors.white, fontWeight: '700', marginTop: 5, maxWidth: 58 }, viewerTime: { fontSize: 8, color: '#C9C3D8', marginTop: 2 }, emptyViewers: { color: '#D8D2F1', fontSize: 11, paddingVertical: 17 },
});
