import { useEffect, useState } from 'react';
import { Pressable, StyleSheet, Text, View } from 'react-native';
import { router, useLocalSearchParams } from 'expo-router';
import { useQuery } from '@tanstack/react-query';
import { useVideoPlayer, VideoView } from 'expo-video';
import { X } from 'lucide-react-native';
import { API_URL, sessionTokens } from '../../api/client';
import { profileApi, storyApi } from '../../api/mindmatch';
import { Avatar, Loading, ProtectedImage, Screen } from '../../components/ui';
import { colors } from '../../theme/tokens';

export default function StoryScreen() {
  const { id, userId } = useLocalSearchParams<{ id: string; userId?: string }>();
  const [token, setToken] = useState<string | null>(null);
  const person = useQuery({ queryKey: ['profile', userId], queryFn: () => profileApi.get(userId!), enabled: !!userId });
  const story = useQuery({ queryKey: ['story', id], queryFn: () => storyApi.get(id) });
  const player = useVideoPlayer(null);

  useEffect(() => {
    sessionTokens.access().then(setToken);
  }, []);
  useEffect(() => {
    storyApi.view(id).catch(() => {});
  }, [id]);
  useEffect(() => {
    const mediaId = story.data?.mediaAssetId;
    if (story.data?.mediaType !== 2 || !mediaId || !token) return;
    player.replaceAsync({ uri: `${API_URL}/api/media/${mediaId}`, headers: { Authorization: `Bearer ${token}` } })
      .then(() => player.play())
      .catch(() => {});
    return () => player.pause();
  }, [story.data?.mediaAssetId, story.data?.mediaType, token, player]);

  if (story.isLoading) return <Screen><Loading label="Hikâye açılıyor" /></Screen>;
  const isVideo = story.data?.mediaType === 2;
  return (
    <View style={styles.root}>
      {isVideo && story.data?.mediaAssetId ? <VideoView player={player} style={StyleSheet.absoluteFill} contentFit="contain" nativeControls /> :
        story.data?.mediaAssetId ? <ProtectedImage mediaAssetId={story.data.mediaAssetId} style={StyleSheet.absoluteFill} contentFit="contain" /> : <View style={styles.blank} />}
      <View style={styles.scrim} pointerEvents="none" />
      <Pressable onPress={() => router.back()} style={styles.close}><X size={21} color={colors.white} /></Pressable>
      <View style={styles.top}><Avatar name={person.data?.displayName || 'MindMatch'} size={40} /><View><Text style={styles.user}>{person.data?.displayName || 'MindMatch kullanıcısı'}</Text><Text style={styles.date}>{story.data ? new Date(story.data.createdAtUtc).toLocaleString('tr-TR') : ''}</Text></View></View>
      {story.data?.caption ? <Text style={styles.caption}>{story.data.caption}</Text> : null}
    </View>
  );
}

const styles = StyleSheet.create({
  root: { flex: 1, backgroundColor: '#111018', justifyContent: 'center' }, blank: { ...StyleSheet.absoluteFill, backgroundColor: '#373044' },
  scrim: { ...StyleSheet.absoluteFill, backgroundColor: '#00000012' },
  close: { position: 'absolute', zIndex: 2, top: 54, right: 18, width: 42, height: 42, borderRadius: 15, alignItems: 'center', justifyContent: 'center', backgroundColor: '#17132999' },
  top: { position: 'absolute', top: 56, left: 18, flexDirection: 'row', alignItems: 'center', gap: 10 },
  user: { fontSize: 12, color: colors.white, fontWeight: '800' }, date: { fontSize: 9, color: '#FFFFFFC0', marginTop: 4 },
  caption: { position: 'absolute', bottom: 60, alignSelf: 'center', fontSize: 16, color: colors.white, fontWeight: '700', textShadowColor: '#000', textShadowRadius: 5, paddingHorizontal: 20, textAlign: 'center' },
});
