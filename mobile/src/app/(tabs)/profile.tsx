import { useEffect, useRef, useState } from 'react';
import {
  ActivityIndicator,
  Alert,
  Animated,
  Dimensions,
  Platform,
  Pressable,
  ScrollView,
  StyleSheet,
  Switch,
  Text,
  View,
} from 'react-native';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { router } from 'expo-router';
import * as ImagePicker from 'expo-image-picker';
import * as Haptics from 'expo-haptics';
import {
  Bell,
  Camera,
  Check,
  ChevronRight,
  CirclePlus,
  Crown,
  Eye,
  Flame,
  Globe,
  ImagePlus,
  LogOut,
  MapPin,
  Pencil,
  Plus,
  Settings,
  Shield,
  Sparkles,
  Star,
  Trash2,
  User,
} from 'lucide-react-native';
import { LinearGradient } from 'expo-linear-gradient';
import { interestApi, mediaApi, profileApi, premiumApi, storyApi, notificationApi } from '../../api/mindmatch';
import { Avatar, Button, Card, Loading, ProtectedImage, Screen } from '../../components/ui';
import { useSession } from '../../state/session';
import { colors } from '../../theme/tokens';
import { normalizeImageForUpload, uploadFailureMessage } from '../../utils/media';

const { width: SCREEN_WIDTH } = Dimensions.get('window');

export default function ProfileScreen() {
  const qc = useQueryClient();
  const user = useSession(x => x.user);
  const signOut = useSession(x => x.signOut);

  const [uploadingPhoto, setUploadingPhoto] = useState(false);
  const [photoUploadStage, setPhotoUploadStage] = useState('');
  const [photoUploadError, setPhotoUploadError] = useState('');

  const profile = useQuery({ queryKey: ['my-profile'], queryFn: profileApi.mine });
  const photos = useQuery({ queryKey: ['my-photos'], queryFn: () => profileApi.photos() });
  const interests = useQuery({ queryKey: ['interests'], queryFn: interestApi.list });
  const premium = useQuery({ queryKey: ['premium'], queryFn: premiumApi.mine });
  const ownStories = useQuery({ queryKey: ['my-stories'], queryFn: storyApi.mine });
  const notifications = useQuery({ queryKey: ['notifications'], queryFn: notificationApi.list, refetchInterval: 15_000 });

  // Pulsing animation for story ring / VIP aura
  const pulseAnim = useRef(new Animated.Value(1)).current;
  useEffect(() => {
    Animated.loop(
      Animated.sequence([
        Animated.timing(pulseAnim, { toValue: 1.05, duration: 1500, useNativeDriver: true }),
        Animated.timing(pulseAnim, { toValue: 1, duration: 1500, useNativeDriver: true }),
      ])
    ).start();
  }, [pulseAnim]);

  const visible = useMutation({
    mutationFn: profileApi.setVisibility,
    onSuccess: () => {
      Haptics.notificationAsync(Haptics.NotificationFeedbackType.Success).catch(() => {});
      qc.invalidateQueries({ queryKey: ['my-profile'] });
    },
    onError: error => Alert.alert('Görünürlük değiştirilemedi', error.message),
  });

  const setPrimary = useMutation({
    mutationFn: profileApi.setPrimaryPhoto,
    onSuccess: () => {
      Haptics.notificationAsync(Haptics.NotificationFeedbackType.Success).catch(() => {});
      qc.invalidateQueries({ queryKey: ['my-photos'] });
    },
    onError: error => Alert.alert('Fotoğraf güncellenemedi', error.message),
  });

  const deletePhoto = useMutation({
    mutationFn: profileApi.deletePhoto,
    onSuccess: () => {
      Haptics.notificationAsync(Haptics.NotificationFeedbackType.Success).catch(() => {});
      qc.invalidateQueries({ queryKey: ['my-photos'] });
    },
    onError: error => Alert.alert('Fotoğraf silinemedi', error.message),
  });

  async function addPhoto(source: 'camera' | 'library' = 'library') {
    if (uploadingPhoto) return;
    setUploadingPhoto(true);
    setPhotoUploadStage('Fotoğraf seçiliyor…');
    setPhotoUploadError('');
    try {
      const permission =
        source === 'camera'
          ? await ImagePicker.requestCameraPermissionsAsync()
          : await ImagePicker.requestMediaLibraryPermissionsAsync();
      if (!permission.granted) {
        throw new Error(source === 'camera' ? 'Kamera izni gerekli.' : 'Galeri izni gerekli.');
      }
      const options = {
        mediaTypes: ['images'],
        allowsEditing: true,
        aspect: [4, 5] as [number, number],
        quality: 0.9,
        preferredAssetRepresentationMode: ImagePicker.UIImagePickerPreferredAssetRepresentationMode.Compatible,
      };
      const pick =
        source === 'camera'
          ? await ImagePicker.launchCameraAsync(options)
          : await ImagePicker.launchImageLibraryAsync(options);
      if (pick.canceled) return;

      setPhotoUploadStage('Fotoğraf hazırlanıyor…');
      const image = await normalizeImageForUpload(pick.assets[0].uri);

      setPhotoUploadStage('Sunucuya yükleniyor…');
      const media = await mediaApi.upload(image.uri, image.fileName, image.mimeType, 1);

      setPhotoUploadStage('Kaydediliyor…');
      try {
        await mediaApi.photo(media.id);
      } catch (error) {
        await mediaApi.delete(media.id).catch(() => {});
        throw error;
      }
      Haptics.notificationAsync(Haptics.NotificationFeedbackType.Success).catch(() => {});
      await Promise.all([
        qc.invalidateQueries({ queryKey: ['my-photos'] }),
        qc.invalidateQueries({ queryKey: ['my-profile'] }),
      ]);
      Alert.alert('Fotoğraf Eklendi', 'Yeni fotoğrafın vitrine başarıyla eklendi.');
    } catch (error) {
      const message = uploadFailureMessage(error);
      setPhotoUploadError(message);
      if (Platform.OS !== 'web') Alert.alert('Fotoğraf yüklenemedi', message);
    } finally {
      setUploadingPhoto(false);
      setPhotoUploadStage('');
    }
  }

  function choosePhoto() {
    if (Platform.OS === 'web') {
      addPhoto();
      return;
    }
    Alert.alert('Profil Vitrini', 'Yeni bir fotoğraf ekle:', [
      { text: '📷 Kamera ile Çek', onPress: () => addPhoto('camera') },
      { text: '🖼️ Galeriden Seç', onPress: () => addPhoto('library') },
      { text: 'Vazgeç', style: 'cancel' },
    ]);
  }

  function photoActions(photo: { id: string; isPrimary: boolean }) {
    Alert.alert(
      'Fotoğraf Seçenekleri',
      photo.isPrimary ? 'Bu senin ana profil fotoğrafın.' : 'Bu fotoğraf için ne yapmak istersin?',
      [
        ...(!photo.isPrimary ? [{ text: '★ Ana Fotoğraf Yap', onPress: () => setPrimary.mutate(photo.id) }] : []),
        {
          text: 'Fotoğrafı Sil',
          style: 'destructive',
          onPress: () =>
            Alert.alert('Silinsin mi?', 'Bu fotoğraf kalıcı olarak kaldırılacak.', [
              { text: 'Vazgeç', style: 'cancel' },
              { text: 'Sil', style: 'destructive', onPress: () => deletePhoto.mutate(photo.id) },
            ]),
        },
        { text: 'Vazgeç', style: 'cancel' },
      ]
    );
  }

  if (profile.isLoading) {
    return (
      <Screen>
        <Loading label="Profil yükleniyor…" />
      </Screen>
    );
  }

  const p = profile.data;
  const photoList = photos.data ?? [];
  const primaryPhoto = photoList.find(photo => photo.isPrimary) ?? photoList[0];
  const activeStories = ownStories.data ?? [];
  const hasActiveStories = activeStories.length > 0;
  const isVip = Boolean(premium.data?.isPremium);
  const unreadCount = notifications.data?.items?.filter(x => !x.readAtUtc).length ?? 0;

  const selectedInterestNames = (p?.interestIds ?? [])
    .map(id => interests.data?.find(item => item.id === id)?.name)
    .filter((name): name is string => Boolean(name));

  return (
    <Screen scroll style={styles.screen}>
      {/* Snapchat-Style Top Bar (No clunky card, floating clean header) */}
      <View style={styles.snapTopBar}>
        <Pressable
          accessibilityRole="button"
          onPress={() => router.push('/notifications')}
          style={styles.headerIconButton}
        >
          <Bell size={20} color={colors.ink} />
          {unreadCount > 0 && <View style={styles.bellBadge} />}
        </Pressable>

        <Text style={styles.topBarTitle}>Profil</Text>

        <Pressable
          accessibilityRole="button"
          onPress={() => router.push('/settings')}
          style={styles.headerIconButton}
        >
          <Settings size={20} color={colors.ink} />
        </Pressable>
      </View>

      {/* Snapchat Hero Section: Centered, Fluid, Bitmoji / Snapcode style */}
      <View style={styles.snapHero}>
        {/* Glowing / Burning Avatar Container */}
        <Animated.View style={{ transform: [{ scale: hasActiveStories ? pulseAnim : 1 }] }}>
          <Pressable
            accessibilityRole="button"
            onPress={() => {
              if (hasActiveStories) {
                router.push({ pathname: '/story/[id]', params: { id: activeStories[0].id, userId: p?.userId } });
              } else {
                choosePhoto();
              }
            }}
            style={styles.avatarTouchTarget}
          >
            {hasActiveStories ? (
              <LinearGradient
                colors={['#FF007A', '#FF4500', '#FFD600', '#FF0055']}
                start={{ x: 0, y: 0 }}
                end={{ x: 1, y: 1 }}
                style={styles.snapHeroRing}
              >
                <View style={styles.snapHeroInner}>
                  <Avatar name={p?.displayName || 'Sen'} size={92} mediaAssetId={primaryPhoto?.mediaAssetId} />
                </View>
                <View style={styles.snapStoryCountBadge}>
                  <Text style={styles.snapStoryCountText}>{activeStories.length}</Text>
                </View>
              </LinearGradient>
            ) : (
              <View style={[styles.snapNormalRing, isVip && styles.snapVipRing]}>
                <Avatar name={p?.displayName || 'Sen'} size={92} mediaAssetId={primaryPhoto?.mediaAssetId} />
                <Pressable onPress={choosePhoto} style={styles.snapAddBadge}>
                  <Plus size={16} color="#FFF" strokeWidth={3} />
                </Pressable>
              </View>
            )}
          </Pressable>
        </Animated.View>

        {/* Display Name + Badges */}
        <View style={styles.snapNameRow}>
          <Text style={styles.snapDisplayName}>{p?.displayName || 'MindMatch Üyesi'}</Text>
          {isVip && (
            <LinearGradient
              colors={['#FFDF00', '#F59E0B']}
              style={styles.snapVipBadge}
            >
              <Crown size={12} color="#FFF" fill="#FFF" />
              <Text style={styles.snapVipBadgeText}>VIP</Text>
            </LinearGradient>
          )}
        </View>

        {/* Username & City Details */}
        <Text style={styles.snapHandleText}>
          @{p?.nickname || 'mindmatch'}
          {p?.age ? ` · ${p.age} yaş` : ''}
          {p?.city ? ` · ${p.city}` : ''}
        </Text>

        {/* Bio Status Quote */}
        {p?.bio?.trim() ? (
          <Text style={styles.snapBioText}>"{p.bio.trim()}"</Text>
        ) : (
          <Pressable onPress={() => router.push('/settings')}>
            <Text style={styles.snapBioPlaceholder}>+ Biyografi ve durumunu ekle</Text>
          </Pressable>
        )}

        {/* Snapchat Action Pills (Horizontal Row) */}
        <View style={styles.snapActionPillsRow}>
          <Pressable
            onPress={() => router.push('/(tabs)/stories')}
            style={styles.snapPillPrimary}
          >
            <Camera size={16} color="#FFF" />
            <Text style={styles.snapPillPrimaryText}>Hikâye Paylaş</Text>
          </Pressable>

          <Pressable
            onPress={() => router.push('/settings')}
            style={styles.snapPillSecondary}
          >
            <Pencil size={15} color={colors.ink} />
            <Text style={styles.snapPillSecondaryText}>Profili Düzenle</Text>
          </Pressable>

          <Pressable
            onPress={() => router.push('/(tabs)/notifications')}
            style={[styles.snapPillSecondary, isVip && styles.snapPillVip]}
          >
            <Crown size={15} color={isVip ? '#B45309' : colors.violet} />
            <Text style={[styles.snapPillSecondaryText, isVip && { color: '#B45309' }]}>
              {isVip ? 'VIP Üye' : 'VIP Ol'}
            </Text>
          </Pressable>
        </View>
      </View>

      {/* Snapchat Section 1: My Stories Card */}
      <View style={styles.sectionCard}>
        <View style={styles.sectionHeader}>
          <Text style={styles.sectionTitle}>Hikâyelerim</Text>
          <Pressable onPress={() => router.push('/(tabs)/stories')}>
            <Text style={styles.sectionActionText}>Tümünü Gör →</Text>
          </Pressable>
        </View>

        {hasActiveStories ? (
          <Pressable
            onPress={() =>
              router.push({ pathname: '/story/[id]', params: { id: activeStories[0].id, userId: p?.userId } })
            }
            style={styles.activeStoryRow}
          >
            <View style={styles.storyThumbWrapper}>
              {activeStories[0].mediaAssetId ? (
                <ProtectedImage mediaAssetId={activeStories[0].mediaAssetId} style={StyleSheet.absoluteFill} />
              ) : (
                <View style={styles.storyThumbFallback}><Text style={{ color: '#FFF' }}>✦</Text></View>
              )}
            </View>

            <View style={{ flex: 1, marginLeft: 12 }}>
              <Text style={styles.activeStoryTitle}>Aktif Hikâyen Yayında</Text>
              <Text style={styles.activeStorySubtitle}>
                {activeStories.length} an paylaşıldı · 24 saat içinde sonlanır
              </Text>
            </View>

            <View style={styles.storyWatchBtn}>
              <Eye size={14} color={colors.violet} />
              <Text style={styles.storyWatchBtnText}>İzle</Text>
            </View>
          </Pressable>
        ) : (
          <Pressable onPress={() => router.push('/(tabs)/stories')} style={styles.noStoryBox}>
            <View style={styles.noStoryPlus}>
              <CirclePlus size={20} color={colors.violet} />
            </View>
            <View style={{ flex: 1, marginLeft: 10 }}>
              <Text style={styles.noStoryTitle}>Henüz hikâye paylaşmadın</Text>
              <Text style={styles.noStorySub}>Günün bir anını arkadaşlarınla paylaş.</Text>
            </View>
          </Pressable>
        )}
      </View>

      {/* Snapchat Section 2: Photo Showcase (Vitrin) */}
      <View style={styles.sectionCard}>
        <View style={styles.sectionHeader}>
          <View style={{ flexDirection: 'row', alignItems: 'center', gap: 6 }}>
            <Text style={styles.sectionTitle}>Fotoğraf Vitrini</Text>
            <Text style={styles.photoCountText}>{photoList.length}/6</Text>
          </View>
          {uploadingPhoto ? (
            <ActivityIndicator size="small" color={colors.violet} />
          ) : (
            <Pressable onPress={choosePhoto}>
              <Text style={styles.sectionActionText}>+ Fotoğraf Ekle</Text>
            </Pressable>
          )}
        </View>

        {uploadingPhoto && <Text style={styles.uploadingNotice}>{photoUploadStage}</Text>}
        {photoUploadError ? <Text style={styles.uploadErrorNotice}>{photoUploadError}</Text> : null}

        <View style={styles.photoGrid}>
          {photoList.map(photo => (
            <Pressable
              key={photo.id}
              onPress={() => photoActions(photo)}
              style={styles.photoGridSlot}
            >
              {photo.mediaAssetId ? (
                <ProtectedImage mediaAssetId={photo.mediaAssetId} style={StyleSheet.absoluteFill} />
              ) : null}

              {photo.isPrimary ? (
                <View style={styles.primaryStarBadge}>
                  <Star size={10} color="#FFF" fill="#FFF" />
                  <Text style={styles.primaryStarText}>ANA</Text>
                </View>
              ) : null}
            </Pressable>
          ))}

          {photoList.length < 6 ? (
            <Pressable onPress={choosePhoto} style={styles.emptyPhotoSlot}>
              <ImagePlus size={22} color={colors.muted} />
              <Text style={styles.emptySlotText}>Ekle</Text>
            </Pressable>
          ) : null}
        </View>
      </View>

      {/* Snapchat Section 3: Interests */}
      {selectedInterestNames.length > 0 && (
        <View style={styles.sectionCard}>
          <Text style={[styles.sectionTitle, { marginBottom: 10 }]}>İlgi Alanlarım</Text>
          <View style={styles.interestChipsRow}>
            {selectedInterestNames.map((name, i) => (
              <View key={i} style={styles.interestChip}>
                <Text style={styles.interestChipText}>#{name}</Text>
              </View>
            ))}
          </View>
        </View>
      )}

      {/* Snapchat Section 4: Settings & Visibility Switch */}
      <View style={styles.sectionCard}>
        <Text style={[styles.sectionTitle, { marginBottom: 12 }]}>Hesap & Gizlilik</Text>

        <View style={styles.groupedCell}>
          <View style={styles.groupedCellLeft}>
            <Globe size={18} color={colors.violet} />
            <View style={{ marginLeft: 12 }}>
              <Text style={styles.cellTitle}>Keşfette Görünürlük</Text>
              <Text style={styles.cellSub}>Profilin açık ve herkes seni bulabilir</Text>
            </View>
          </View>
          <Switch
            value={p?.isProfileVisible}
            onValueChange={val => visible.mutate(val)}
            trackColor={{ false: '#DDD', true: colors.violet }}
            thumbColor="#FFF"
          />
        </View>

        <Pressable onPress={() => router.push('/settings')} style={styles.groupedNavCell}>
          <View style={styles.groupedCellLeft}>
            <Shield size={18} color={colors.ink} />
            <Text style={[styles.cellTitle, { marginLeft: 12 }]}>Gizlilik ve Güvenlik Ayarları</Text>
          </View>
          <ChevronRight size={18} color={colors.muted} />
        </Pressable>

        <Pressable
          onPress={() =>
            Alert.alert('Çıkış Yap', 'Oturumunu kapatmak istediğine emin misin?', [
              { text: 'Vazgeç', style: 'cancel' },
              { text: 'Çıkış Yap', style: 'destructive', onPress: () => signOut() },
            ])
          }
          style={[styles.groupedNavCell, { borderBottomWidth: 0 }]}
        >
          <View style={styles.groupedCellLeft}>
            <LogOut size={18} color={colors.red} />
            <Text style={[styles.cellTitle, { marginLeft: 12, color: colors.red }]}>Oturumu Kapat</Text>
          </View>
        </Pressable>
      </View>
    </Screen>
  );
}

const styles = StyleSheet.create({
  screen: { paddingHorizontal: 16, paddingTop: 10, paddingBottom: 40 },
  snapTopBar: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    marginBottom: 16,
  },
  headerIconButton: {
    width: 42,
    height: 42,
    borderRadius: 21,
    backgroundColor: colors.white,
    borderWidth: 1,
    borderColor: colors.line,
    alignItems: 'center',
    justifyContent: 'center',
  },
  bellBadge: {
    position: 'absolute',
    top: 9,
    right: 9,
    width: 8,
    height: 8,
    borderRadius: 4,
    backgroundColor: colors.coral,
  },
  topBarTitle: {
    fontSize: 18,
    fontWeight: '900',
    color: colors.ink,
    letterSpacing: -0.4,
  },
  snapHero: {
    alignItems: 'center',
    paddingVertical: 14,
    marginBottom: 20,
  },
  avatarTouchTarget: {
    position: 'relative',
    marginBottom: 12,
  },
  snapHeroRing: {
    width: 106,
    height: 106,
    borderRadius: 53,
    padding: 3.5,
    alignItems: 'center',
    justifyContent: 'center',
    shadowColor: '#FF4500',
    shadowOpacity: 0.5,
    shadowRadius: 10,
    elevation: 8,
  },
  snapHeroInner: {
    width: 98,
    height: 98,
    borderRadius: 49,
    backgroundColor: colors.white,
    alignItems: 'center',
    justifyContent: 'center',
  },
  snapStoryCountBadge: {
    position: 'absolute',
    top: 2,
    right: 2,
    backgroundColor: '#FF007A',
    paddingHorizontal: 7,
    paddingVertical: 2,
    borderRadius: 10,
    borderWidth: 2,
    borderColor: colors.white,
  },
  snapStoryCountText: { color: '#FFF', fontSize: 10, fontWeight: '900' },
  snapNormalRing: {
    width: 102,
    height: 102,
    borderRadius: 51,
    borderWidth: 2,
    borderColor: colors.line,
    alignItems: 'center',
    justifyContent: 'center',
  },
  snapVipRing: {
    borderColor: '#FFDF00',
    shadowColor: '#FFDF00',
    shadowOpacity: 0.5,
    shadowRadius: 10,
    elevation: 6,
  },
  snapAddBadge: {
    position: 'absolute',
    bottom: 2,
    right: 2,
    width: 28,
    height: 28,
    borderRadius: 14,
    backgroundColor: colors.violet,
    alignItems: 'center',
    justifyContent: 'center',
    borderWidth: 2,
    borderColor: colors.white,
  },
  snapNameRow: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 8,
    marginBottom: 4,
  },
  snapDisplayName: {
    fontSize: 24,
    fontWeight: '900',
    letterSpacing: -0.6,
    color: colors.ink,
  },
  snapVipBadge: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 3,
    paddingHorizontal: 8,
    paddingVertical: 3,
    borderRadius: 12,
  },
  snapVipBadgeText: { color: '#FFF', fontSize: 10, fontWeight: '900' },
  snapHandleText: {
    fontSize: 13,
    color: colors.muted,
    fontWeight: '600',
    marginBottom: 8,
  },
  snapBioText: {
    fontSize: 14,
    lineHeight: 20,
    color: colors.ink,
    textAlign: 'center',
    paddingHorizontal: 24,
    fontStyle: 'italic',
    marginBottom: 16,
  },
  snapBioPlaceholder: {
    fontSize: 13,
    color: colors.violet,
    fontWeight: '700',
    marginBottom: 16,
  },
  snapActionPillsRow: {
    flexDirection: 'row',
    gap: 10,
    marginTop: 4,
  },
  snapPillPrimary: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 6,
    backgroundColor: colors.violet,
    paddingVertical: 10,
    paddingHorizontal: 16,
    borderRadius: 20,
  },
  snapPillPrimaryText: { color: '#FFF', fontSize: 13, fontWeight: '800' },
  snapPillSecondary: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 6,
    backgroundColor: colors.white,
    borderWidth: 1,
    borderColor: colors.line,
    paddingVertical: 10,
    paddingHorizontal: 16,
    borderRadius: 20,
  },
  snapPillSecondaryText: { color: colors.ink, fontSize: 13, fontWeight: '800' },
  snapPillVip: {
    backgroundColor: '#FFFDF0',
    borderColor: '#FFDF00',
  },
  sectionCard: {
    backgroundColor: colors.white,
    borderRadius: 24,
    padding: 18,
    borderWidth: 1,
    borderColor: colors.line,
    marginBottom: 16,
  },
  sectionHeader: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    marginBottom: 12,
  },
  sectionTitle: { fontSize: 16, fontWeight: '900', color: colors.ink },
  sectionActionText: { fontSize: 12, fontWeight: '800', color: colors.violet },
  photoCountText: { fontSize: 12, color: colors.muted, fontWeight: '700' },
  activeStoryRow: {
    flexDirection: 'row',
    alignItems: 'center',
    padding: 6,
  },
  storyThumbWrapper: {
    width: 50,
    height: 60,
    borderRadius: 12,
    overflow: 'hidden',
    backgroundColor: '#1E1B2E',
  },
  storyThumbFallback: { flex: 1, alignItems: 'center', justifyContent: 'center' },
  activeStoryTitle: { fontSize: 14, fontWeight: '900', color: colors.ink },
  activeStorySubtitle: { fontSize: 11, color: colors.muted, marginTop: 2 },
  storyWatchBtn: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 4,
    backgroundColor: colors.paleViolet,
    paddingHorizontal: 10,
    paddingVertical: 6,
    borderRadius: 12,
  },
  storyWatchBtnText: { fontSize: 11, fontWeight: '800', color: colors.violet },
  noStoryBox: {
    flexDirection: 'row',
    alignItems: 'center',
    paddingVertical: 8,
  },
  noStoryPlus: {
    width: 44,
    height: 44,
    borderRadius: 14,
    backgroundColor: colors.paleViolet,
    alignItems: 'center',
    justifyContent: 'center',
  },
  noStoryTitle: { fontSize: 14, fontWeight: '800', color: colors.ink },
  noStorySub: { fontSize: 11, color: colors.muted, marginTop: 2 },
  uploadingNotice: { fontSize: 11, color: colors.violet, fontWeight: '700', marginBottom: 8 },
  uploadErrorNotice: { fontSize: 11, color: colors.red, marginBottom: 8 },
  photoGrid: {
    flexDirection: 'row',
    flexWrap: 'wrap',
    gap: 8,
  },
  photoGridSlot: {
    width: (SCREEN_WIDTH - 32 - 36 - 16) / 3,
    aspectRatio: 0.85,
    borderRadius: 14,
    overflow: 'hidden',
    backgroundColor: colors.paleViolet,
    borderWidth: 1,
    borderColor: colors.line,
  },
  emptyPhotoSlot: {
    width: (SCREEN_WIDTH - 32 - 36 - 16) / 3,
    aspectRatio: 0.85,
    borderRadius: 14,
    borderWidth: 1.5,
    borderStyle: 'dashed',
    borderColor: '#D4CEE8',
    alignItems: 'center',
    justifyContent: 'center',
    gap: 4,
  },
  emptySlotText: { fontSize: 11, fontWeight: '800', color: colors.muted },
  primaryStarBadge: {
    position: 'absolute',
    top: 6,
    left: 6,
    flexDirection: 'row',
    alignItems: 'center',
    gap: 3,
    backgroundColor: 'rgba(23, 19, 41, 0.75)',
    paddingHorizontal: 6,
    paddingVertical: 2,
    borderRadius: 6,
  },
  primaryStarText: { color: '#FFF', fontSize: 8, fontWeight: '900' },
  interestChipsRow: { flexDirection: 'row', flexWrap: 'wrap', gap: 8 },
  interestChip: {
    backgroundColor: colors.canvas,
    borderWidth: 1,
    borderColor: colors.line,
    paddingHorizontal: 12,
    paddingVertical: 6,
    borderRadius: 14,
  },
  interestChipText: { fontSize: 12, fontWeight: '700', color: colors.muted },
  groupedCell: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    paddingVertical: 12,
    borderBottomWidth: 1,
    borderBottomColor: colors.line,
  },
  groupedNavCell: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    paddingVertical: 14,
    borderBottomWidth: 1,
    borderBottomColor: colors.line,
  },
  groupedCellLeft: { flexDirection: 'row', alignItems: 'center' },
  cellTitle: { fontSize: 14, fontWeight: '800', color: colors.ink },
  cellSub: { fontSize: 11, color: colors.muted, marginTop: 2 },
});
