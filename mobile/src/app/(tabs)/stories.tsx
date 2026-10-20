import { useMemo, useState } from 'react';
import {
  Alert,
  Dimensions,
  Platform,
  Pressable,
  ScrollView,
  StyleSheet,
  Text,
  View,
} from 'react-native';
import * as ImagePicker from 'expo-image-picker';
import * as Haptics from 'expo-haptics';
import { useQueries, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  Camera,
  CirclePlus,
  Eye,
  ImagePlus,
  Play,
  RefreshCw,
  Sparkles,
  Users,
} from 'lucide-react-native';
import { router } from 'expo-router';
import { LinearGradient } from 'expo-linear-gradient';
import { mediaApi, profileApi, storyApi } from '../../api/mindmatch';
import { Avatar, Button, Card, Empty, Field, Loading, ProtectedImage, Screen } from '../../components/ui';
import { colors } from '../../theme/tokens';
import { normalizeImageForUpload, uploadFailureMessage } from '../../utils/media';
import type { Story } from '../../api/types';

const { width: SCREEN_WIDTH } = Dimensions.get('window');

export default function StoriesScreen() {
  const qc = useQueryClient();
  const [uploading, setUploading] = useState(false);
  const [uploadStage, setUploadStage] = useState('');
  const [uploadError, setUploadError] = useState('');
  const [caption, setCaption] = useState('');

  // Feed: all public stories from all visible profiles
  const feed = useQuery({ queryKey: ['stories'], queryFn: storyApi.feed, refetchInterval: 15000 });
  const mine = useQuery({ queryKey: ['my-stories'], queryFn: storyApi.mine });
  const myProfile = useQuery({ queryKey: ['my-profile'], queryFn: profileApi.mine });
  const myPhotos = useQuery({ queryKey: ['my-photos'], queryFn: () => profileApi.photos() });
  const myPrimaryPhoto = myPhotos.data?.find(photo => photo.isPrimary) ?? myPhotos.data?.[0];

  // Group stories by userId (like Instagram)
  const storiesByUser = useMemo(() => {
    const map = new Map<string, Story[]>();
    for (const story of feed.data ?? []) {
      const list = map.get(story.userId) || [];
      list.push(story);
      map.set(story.userId, list);
    }
    // Sort each user's stories chronologically
    for (const [uid, list] of map.entries()) {
      list.sort((a, b) => new Date(a.createdAtUtc).getTime() - new Date(b.createdAtUtc).getTime());
    }
    return map;
  }, [feed.data]);

  const ownerIds = Array.from(storiesByUser.keys());
  const people = useQueries({
    queries: ownerIds.map(userId => ({
      queryKey: ['profile', userId],
      queryFn: () => profileApi.get(userId),
    })),
  });
  const peopleById = useMemo(() => {
    return new Map(ownerIds.map((userId, index) => [userId, people[index]?.data]));
  }, [ownerIds, people]);

  async function addStory(source: 'camera' | 'library' = 'library') {
    setUploading(true);
    setUploadStage('Medya seçiliyor…');
    setUploadError('');
    try {
      const permission =
        source === 'camera'
          ? await ImagePicker.requestCameraPermissionsAsync()
          : await ImagePicker.requestMediaLibraryPermissionsAsync();
      if (!permission.granted) {
        throw new Error(
          source === 'camera' ? 'Kamera izni gerekli.' : 'Galeri izni gerekli.'
        );
      }
      const options = {
        mediaTypes: ['images', 'videos'],
        quality: 0.85,
        videoMaxDuration: 30,
        videoExportPreset: ImagePicker.VideoExportPreset.H264_1280x720,
        preferredAssetRepresentationMode: ImagePicker.UIImagePickerPreferredAssetRepresentationMode.Compatible,
      };
      const result =
        source === 'camera'
          ? await ImagePicker.launchCameraAsync(options)
          : await ImagePicker.launchImageLibraryAsync(options);
      if (result.canceled) return;
      const asset = result.assets[0];
      const isVideo = asset.type === 'video';
      setUploadStage('Medya işleniyor…');
      const normalized = isVideo
        ? { uri: asset.uri, fileName: 'mindmatch-story.mp4', mimeType: 'video/mp4' }
        : await normalizeImageForUpload(asset.uri);

      setUploadStage('Yükleniyor…');
      const media = await mediaApi.upload(normalized.uri, normalized.fileName, normalized.mimeType, isVideo ? 5 : 4);
      try {
        setUploadStage('Hikâye yayınlanıyor…');
        await storyApi.create(media.id, isVideo ? 2 : 1, caption.trim() || undefined);
      } catch (error) {
        await mediaApi.delete(media.id).catch(() => {});
        throw error;
      }
      Haptics.notificationAsync(Haptics.NotificationFeedbackType.Success).catch(() => {});
      setCaption('');
      await Promise.all([
        qc.invalidateQueries({ queryKey: ['stories'] }),
        qc.invalidateQueries({ queryKey: ['my-stories'] }),
      ]);
      Alert.alert('Hikâyen Yayında!', 'Hikâyen tüm keşfedenlere 24 saat boyunca açık kalacaktır.');
    } catch (error) {
      const message = uploadFailureMessage(error);
      setUploadError(message);
      if (Platform.OS !== 'web') Alert.alert('Hikâye paylaşılamadı', message);
    } finally {
      setUploading(false);
      setUploadStage('');
    }
  }

  function chooseStory() {
    if (Platform.OS === 'web') {
      addStory();
      return;
    }
    Alert.alert('Hikâye Paylaş', 'Yeni bir an ekle:', [
      { text: '📷 Kamera ile Çek', onPress: () => addStory('camera') },
      { text: '🖼️ Galeriden Seç', onPress: () => addStory('library') },
      { text: 'Vazgeç', style: 'cancel' },
    ]);
  }

  function openUserStory(userId: string, targetStoryId?: string) {
    const list = storiesByUser.get(userId) || [];
    const firstId = targetStoryId || list[0]?.id;
    if (!firstId) return;
    router.push({ pathname: '/story/[id]', params: { id: firstId, userId } });
  }

  function openMyStory() {
    if (!mine.data?.length) {
      chooseStory();
      return;
    }
    router.push({ pathname: '/story/[id]', params: { id: mine.data[0].id, userId: myProfile.data?.userId } });
  }

  function formatTimeAgo(dateStr: string) {
    const diffMs = Date.now() - new Date(dateStr).getTime();
    const diffMin = Math.floor(diffMs / 60000);
    const diffHours = Math.floor(diffMin / 60);
    if (diffMin < 1) return 'Az önce';
    if (diffMin < 60) return `${diffMin} dk önce`;
    if (diffHours < 24) return `${diffHours} sa önce`;
    return 'Dün';
  }

  if (feed.isLoading) {
    return (
      <Screen>
        <Loading label="Hikâyeler yükleniyor…" />
      </Screen>
    );
  }

  return (
    <Screen scroll style={styles.screen}>
      {/* Top Header */}
      <View style={styles.topHeader}>
        <View>
          <Text style={styles.eyebrow}>ANLIK & HERKESE AÇIK</Text>
          <Text style={styles.pageTitle}>Hikâyeler</Text>
        </View>
        <Pressable
          style={styles.refreshBtn}
          onPress={() => {
            Haptics.selectionAsync().catch(() => {});
            feed.refetch();
          }}
        >
          <RefreshCw size={17} color={colors.violet} strokeWidth={2.2} />
        </Pressable>
      </View>

      {/* Instagram-Style Story Tray (Horizontal Scroll) */}
      <ScrollView
        horizontal
        showsHorizontalScrollIndicator={false}
        contentContainerStyle={styles.storyTray}
      >
        {/* My Story Avatar */}
        <Pressable style={styles.storyCircleWrap} onPress={openMyStory}>
          <View style={[styles.avatarBorder, mine.data?.length ? styles.hasStoriesBorder : styles.noStoriesBorder]}>
            <Avatar
              name={myProfile.data?.displayName || 'Sen'}
              mediaAssetId={myPrimaryPhoto?.mediaAssetId}
              size={60}
            />
            <View style={styles.myStoryPlus}>
              <CirclePlus size={18} color={colors.white} fill={colors.violet} />
            </View>
          </View>
          <Text style={styles.storyCircleName} numberOfLines={1}>
            {mine.data?.length ? 'Hikâyen' : 'Ekle'}
          </Text>
        </Pressable>

        {/* Other Users' Story Circles */}
        {ownerIds.map(userId => {
          const list = storiesByUser.get(userId) || [];
          const profile = peopleById.get(userId);
          const hasUnviewed = list.some(s => !s.hasViewed);

          return (
            <Pressable
              key={userId}
              style={styles.storyCircleWrap}
              onPress={() => openUserStory(userId)}
            >
              {hasUnviewed ? (
                <LinearGradient
                  colors={['#FF007A', '#7928CA', '#FF5400']}
                  start={{ x: 0, y: 0 }}
                  end={{ x: 1, y: 1 }}
                  style={styles.gradientRing}
                >
                  <View style={styles.innerRing}>
                    <Avatar name={profile?.displayName || 'Üye'} size={56} />
                  </View>
                </LinearGradient>
              ) : (
                <View style={[styles.avatarBorder, styles.viewedBorder]}>
                  <Avatar name={profile?.displayName || 'Üye'} size={56} />
                </View>
              )}

              <Text style={styles.storyCircleName} numberOfLines={1}>
                {profile?.displayName || 'Kullanıcı'}
              </Text>
              {list.length > 1 && (
                <View style={styles.storyCountBadge}>
                  <Text style={styles.storyCountText}>{list.length}</Text>
                </View>
              )}
            </Pressable>
          );
        })}
      </ScrollView>

      {/* Share Box Card */}
      <Card style={styles.shareCard}>
        <View style={styles.shareHeader}>
          <Avatar
            name={myProfile.data?.displayName || 'Sen'}
            mediaAssetId={myPrimaryPhoto?.mediaAssetId}
            size={44}
          />
          <View style={{ flex: 1, marginLeft: 12 }}>
            <Text style={styles.shareTitle}>Bugünden bir an paylaş</Text>
            <Text style={styles.shareSub}>
              {mine.data?.length
                ? `${mine.data.length} aktif hikâyen yayında`
                : 'Bütün keşfedenler 24 saat görebilir'}
            </Text>
          </View>
        </View>

        <Field
          label="Hikâye notu ekle (isteğe bağlı)"
          value={caption}
          onChangeText={setCaption}
          placeholder="Aklından geçen kısa bir his veya not..."
          maxLength={150}
          style={styles.captionInput}
        />

        {uploading && <Text style={styles.uploadStageText}>{uploadStage}</Text>}
        {uploadError ? <Text style={styles.uploadErrorText}>{uploadError}</Text> : null}

        <View style={styles.shareButtonsRow}>
          <Button
            title="Kamera / Galeri ile Paylaş"
            icon={<Camera size={18} color={colors.white} />}
            onPress={chooseStory}
            loading={uploading}
            style={{ flex: 1 }}
          />
        </View>
      </Card>

      {/* Feed Section Title */}
      <View style={styles.feedHeaderRow}>
        <View style={{ flexDirection: 'row', alignItems: 'center', gap: 6 }}>
          <Sparkles size={16} color={colors.violet} />
          <Text style={styles.feedSectionTitle}>Tüm Açık Hikâyeler</Text>
        </View>
        <Text style={styles.feedCountLabel}>{feed.data?.length || 0} an yayında</Text>
      </View>

      {/* Story Cards List: Grouped by User */}
      {ownerIds.length > 0 ? (
        <View style={styles.storyCardsList}>
          {ownerIds.map(userId => {
            const list = storiesByUser.get(userId) || [];
            const profile = peopleById.get(userId);
            const latestStory = list[list.length - 1];
            const hasUnviewed = list.some(s => !s.hasViewed);

            return (
              <Pressable
                key={userId}
                onPress={() => openUserStory(userId)}
                style={({ pressed }) => [styles.userStoryCard, pressed && { opacity: 0.9 }]}
              >
                {/* User Header */}
                <View style={styles.cardUserHeader}>
                  <Avatar name={profile?.displayName || 'Üye'} size={42} />
                  <View style={{ flex: 1, marginLeft: 10 }}>
                    <View style={{ flexDirection: 'row', alignItems: 'center', gap: 6 }}>
                      <Text style={styles.cardUserName}>{profile?.displayName || 'Kullanıcı'}</Text>
                      {hasUnviewed && <View style={styles.newBadge}><Text style={styles.newBadgeText}>YENİ</Text></View>}
                    </View>
                    <Text style={styles.cardTimeText}>
                      {list.length} hikâye · Son: {formatTimeAgo(latestStory.createdAtUtc)}
                    </Text>
                  </View>
                  <Text style={styles.watchPrompt}>İzle →</Text>
                </View>

                {/* Preview Thumbnail */}
                <View style={styles.cardThumbnailWrap}>
                  {latestStory.mediaAssetId ? (
                    <ProtectedImage mediaAssetId={latestStory.mediaAssetId} style={StyleSheet.absoluteFill} />
                  ) : (
                    <View style={styles.thumbFallback}>
                      <Text style={{ fontSize: 32, color: '#FFF' }}>✦</Text>
                    </View>
                  )}
                  <LinearGradient
                    colors={['transparent', 'rgba(0,0,0,0.7)']}
                    style={StyleSheet.absoluteFill}
                  />

                  {latestStory.mediaType === 2 && (
                    <View style={styles.playBadge}>
                      <Play size={16} color={colors.white} fill={colors.white} />
                    </View>
                  )}

                  {latestStory.caption ? (
                    <Text style={styles.cardCaption} numberOfLines={2}>
                      "{latestStory.caption}"
                    </Text>
                  ) : null}
                </View>
              </Pressable>
            );
          })}
        </View>
      ) : (
        <Card style={styles.emptyCard}>
          <Empty
            icon="☼"
            title="Henüz aktif hikâye yok"
            detail="İlk anı sen paylaş! Açık profili olan tüm kullanıcılar hikâyeni keşfette görebilir."
            action={
              <Button
                title="Hikâye Paylaş"
                onPress={chooseStory}
                style={{ marginTop: 14 }}
              />
            }
          />
        </Card>
      )}
    </Screen>
  );
}

const styles = StyleSheet.create({
  screen: { paddingHorizontal: 16, paddingTop: 10, paddingBottom: 40 },
  topHeader: { flexDirection: 'row', justifyContent: 'space-between', alignItems: 'center', marginBottom: 14 },
  eyebrow: { fontSize: 10, letterSpacing: 1.5, fontWeight: '900', color: colors.violet },
  pageTitle: { fontSize: 28, fontWeight: '900', letterSpacing: -0.8, color: colors.ink, marginTop: 2 },
  refreshBtn: { width: 40, height: 40, borderRadius: 14, backgroundColor: colors.white, borderWidth: 1, borderColor: colors.line, alignItems: 'center', justifyContent: 'center' },
  storyTray: { flexDirection: 'row', gap: 14, paddingVertical: 10, marginBottom: 16 },
  storyCircleWrap: { alignItems: 'center', width: 68 },
  avatarBorder: { width: 66, height: 66, borderRadius: 33, alignItems: 'center', justifyContent: 'center', borderWidth: 2 },
  hasStoriesBorder: { borderColor: colors.violet },
  noStoriesBorder: { borderColor: colors.line },
  viewedBorder: { borderColor: '#DDD' },
  gradientRing: { width: 68, height: 68, borderRadius: 34, padding: 2.5, alignItems: 'center', justifyContent: 'center' },
  innerRing: { width: 63, height: 63, borderRadius: 31.5, backgroundColor: colors.canvas, alignItems: 'center', justifyContent: 'center' },
  myStoryPlus: { position: 'absolute', bottom: -2, right: -2 },
  storyCircleName: { fontSize: 11, fontWeight: '700', color: colors.ink, marginTop: 6, textAlign: 'center', width: 66 },
  storyCountBadge: { position: 'absolute', top: 0, right: 4, backgroundColor: colors.coral, borderRadius: 8, paddingHorizontal: 5, paddingVertical: 1 },
  storyCountText: { color: colors.white, fontSize: 9, fontWeight: '900' },
  shareCard: { padding: 16, borderRadius: 20, marginBottom: 20 },
  shareHeader: { flexDirection: 'row', alignItems: 'center', marginBottom: 12 },
  shareTitle: { fontSize: 15, fontWeight: '900', color: colors.ink },
  shareSub: { fontSize: 12, color: colors.muted, marginTop: 2 },
  captionInput: { marginTop: 4 },
  uploadStageText: { fontSize: 11, color: colors.violet, fontWeight: '700', marginTop: 8 },
  uploadErrorText: { fontSize: 11, color: colors.red, marginTop: 8 },
  shareButtonsRow: { marginTop: 12 },
  feedHeaderRow: { flexDirection: 'row', justifyContent: 'space-between', alignItems: 'center', marginBottom: 14 },
  feedSectionTitle: { fontSize: 16, fontWeight: '900', color: colors.ink },
  feedCountLabel: { fontSize: 12, color: colors.muted, fontWeight: '700' },
  storyCardsList: { gap: 14 },
  userStoryCard: { backgroundColor: colors.white, borderRadius: 20, borderWidth: 1, borderColor: colors.line, padding: 14, overflow: 'hidden' },
  cardUserHeader: { flexDirection: 'row', alignItems: 'center', marginBottom: 12 },
  cardUserName: { fontSize: 15, fontWeight: '900', color: colors.ink },
  newBadge: { backgroundColor: '#FFF0F2', paddingHorizontal: 6, paddingVertical: 2, borderRadius: 6 },
  newBadgeText: { color: colors.coral, fontSize: 9, fontWeight: '900' },
  cardTimeText: { fontSize: 11, color: colors.muted, marginTop: 2 },
  watchPrompt: { fontSize: 12, color: colors.violet, fontWeight: '800' },
  cardThumbnailWrap: { height: 200, borderRadius: 16, overflow: 'hidden', backgroundColor: '#1A1824', justifyContent: 'flex-end', padding: 14 },
  thumbFallback: { ...StyleSheet.absoluteFillObject, alignItems: 'center', justifyContent: 'center', backgroundColor: colors.paleViolet },
  playBadge: { position: 'absolute', top: 12, right: 12, width: 32, height: 32, borderRadius: 16, backgroundColor: 'rgba(0,0,0,0.6)', alignItems: 'center', justifyContent: 'center' },
  cardCaption: { color: colors.white, fontSize: 14, fontWeight: '700', textShadowColor: '#000', textShadowRadius: 4 },
  emptyCard: { padding: 24, borderRadius: 20 },
});
