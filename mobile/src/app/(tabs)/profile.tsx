import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { router } from 'expo-router';
import * as ImagePicker from 'expo-image-picker';
import { ActivityIndicator, Alert, Platform, Pressable, StyleSheet, Text, View } from 'react-native';
import { Camera, Check, ChevronRight, Crown, LogOut, MapPin, Pencil, Settings, Shield, Sparkles, Star } from 'lucide-react-native';
import { interestApi, mediaApi, profileApi, premiumApi, storyApi } from '../../api/mindmatch';
import { Avatar, Button, Card, Loading, ProtectedImage, Screen, SectionLabel } from '../../components/ui';
import { useSession } from '../../state/session';
import { colors } from '../../theme/tokens';
import { normalizeImageForUpload, uploadFailureMessage } from '../../utils/media';

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
  const visible = useMutation({ mutationFn: profileApi.setVisibility, onSuccess: () => qc.invalidateQueries({ queryKey: ['my-profile'] }), onError: error => Alert.alert('Görünürlük değiştirilemedi', error.message) });
  const setPrimary = useMutation({ mutationFn: profileApi.setPrimaryPhoto, onSuccess: () => qc.invalidateQueries({ queryKey: ['my-photos'] }), onError: error => Alert.alert('Fotoğraf güncellenemedi', error.message) });
  const deletePhoto = useMutation({ mutationFn: profileApi.deletePhoto, onSuccess: () => qc.invalidateQueries({ queryKey: ['my-photos'] }), onError: error => Alert.alert('Fotoğraf silinemedi', error.message) });

  async function addPhoto() {
    if (uploadingPhoto) return;
    setUploadingPhoto(true);
    setPhotoUploadStage('Fotoğraf seçiliyor…');
    setPhotoUploadError('');
    try {
      const pick = await ImagePicker.launchImageLibraryAsync({ mediaTypes: ['images'], quality: .9, preferredAssetRepresentationMode: ImagePicker.UIImagePickerPreferredAssetRepresentationMode.Compatible });
      if (pick.canceled) return;
      setPhotoUploadStage('Fotoğraf hazırlanıyor…');
      const image = await normalizeImageForUpload(pick.assets[0].uri);
      setPhotoUploadStage('Sunucuya yükleniyor…');
      const media = await mediaApi.upload(image.uri, image.fileName, image.mimeType, 1);
      setPhotoUploadStage('Profil fotoğrafı kaydediliyor…');
      try { await mediaApi.photo(media.id); }
      catch (error) { await mediaApi.delete(media.id).catch(() => {}); throw error; }
      await Promise.all([qc.invalidateQueries({ queryKey: ['my-photos'] }), qc.invalidateQueries({ queryKey: ['my-profile'] })]);
      Alert.alert('Fotoğraf eklendi', 'Yeni fotoğrafın profiline eklendi.');
    } catch (error) { const message = uploadFailureMessage(error); setPhotoUploadError(message); if (Platform.OS !== 'web') Alert.alert('Fotoğraf yüklenemedi', message); }
    finally { setUploadingPhoto(false); setPhotoUploadStage(''); }
  }

  function photoActions(photo: { id: string; isPrimary: boolean }) {
    Alert.alert('Fotoğraf seçenekleri', photo.isPrimary ? 'Ana profil fotoğrafın.' : 'Bu fotoğrafla ne yapmak istersin?', [
      { text: 'Vazgeç', style: 'cancel' },
      ...(!photo.isPrimary ? [{ text: 'Ana fotoğraf yap', onPress: () => setPrimary.mutate(photo.id) }] : []),
      { text: 'Sil', style: 'destructive', onPress: () => Alert.alert('Fotoğraf silinsin mi?', 'Bu fotoğraf profilinden kaldırılacak.', [{ text: 'Vazgeç', style: 'cancel' }, { text: 'Sil', style: 'destructive', onPress: () => deletePhoto.mutate(photo.id) }]) },
    ]);
  }

  if (profile.isLoading) return <Screen><Loading label="Profilin yükleniyor" /></Screen>;
  if (profile.error || !profile.data) return <Screen scroll><Card><Text style={s.heading}>Profil açılamadı</Text><Text style={s.muted}>{profile.error instanceof Error ? profile.error.message : 'Profil bilgilerine ulaşılamadı.'}</Text><Button title="Yeniden dene" onPress={() => profile.refetch()} style={s.gap} /></Card></Screen>;

  const p = profile.data;
  const photoList = photos.data ?? [];
  const primaryPhoto = photoList.find(photo => photo.isPrimary) ?? photoList[0];
  const activeStories = ownStories.data ?? [];
  const selectedInterestNames = (p.interestIds ?? []).map(id => interests.data?.find(item => item.id === id)?.name).filter((name): name is string => Boolean(name));
  const completion = [Boolean(p.displayName), Boolean(p.bio?.trim()), Boolean(p.city?.trim()), photoList.length > 0, (p.interestIds?.length ?? 0) > 0].filter(Boolean).length;
  const admin = ['Admin', 'Moderator'].includes(user?.role || '');

  return <Screen scroll style={s.screen}>
    <View style={s.topLine}><View><Text style={s.eyebrow}>MINDMATCH · SENİN ALANIN</Text><Text style={s.pageTitle}>Profilim</Text></View><Pressable accessibilityRole="button" accessibilityLabel="Ayarları aç" onPress={() => router.push('/settings')} style={s.settingsButton}><Settings size={19} color={colors.violet} /></Pressable></View>

    <Card style={s.hero}>
      <View style={s.heroTop}><Pressable accessibilityRole="button" accessibilityLabel={activeStories.length ? 'Hikâyeni görüntüle' : 'Hikâye paylaş'} onPress={() => activeStories.length ? router.push({ pathname: '/story/[id]', params: { id: activeStories[0].id, userId: p.userId } }) : router.push('/(tabs)/stories')} style={[s.avatarRing, activeStories.length > 0 && s.avatarRingActive]}><Avatar name={p.displayName || 'MindMatch'} size={70} mediaAssetId={primaryPhoto?.mediaAssetId} />{activeStories.length > 0 ? <View style={s.storyDot}><Text style={s.storyDotText}>{activeStories.length}</Text></View> : null}</Pressable><View style={s.identity}><Text style={s.name}>{p.displayName || 'MindMatch üyesi'}</Text><Text style={s.handle}>{p.nickname ? `@${p.nickname}` : user?.email}</Text><View style={s.location}><MapPin size={13} color={colors.violet} /><Text style={s.locationText}>{p.city || 'Şehir ekle'}</Text></View></View><Pressable accessibilityRole="button" accessibilityLabel="Profili düzenle" onPress={() => router.push('/settings')} style={s.editButton}><Pencil size={16} color={colors.violet} /></Pressable></View>
      <Text style={s.bio}>{p.bio?.trim() || 'Kendinden bahset. Sana benzeyen insanlarla tanışmanın en güzel yolu bu.'}</Text>
      <View style={s.progressHead}><Text style={s.progressTitle}>Profil tamamlanma</Text><Text style={s.progressValue}>{completion}/5</Text></View><View style={s.progressTrack}><View style={[s.progressFill, { width: `${completion * 20}%` }]} /></View>
      <View style={s.photoSection}><SectionLabel>Fotoğrafların <Text style={s.photoCount}>{photoList.length}/6</Text></SectionLabel><View style={s.photoGrid}>{photoList.map(photo => <Pressable key={photo.id} onPress={() => photoActions(photo)} style={s.photoTile}>{photo.mediaAssetId ? <ProtectedImage mediaAssetId={photo.mediaAssetId} style={StyleSheet.absoluteFill} /> : null}{photo.isPrimary ? <View style={s.primaryBadge}><Star size={10} color={colors.white} fill={colors.white} /><Text style={s.badgeText}>ANA</Text></View> : <View style={s.photoMenu}><Text style={s.photoMenuText}>•••</Text></View>}</Pressable>)}{photoList.length < 6 ? <Pressable accessibilityRole="button" accessibilityLabel={uploadingPhoto ? photoUploadStage : 'Fotoğraf ekle'} disabled={uploadingPhoto} style={[s.addTile, uploadingPhoto && s.disabled]} onPress={addPhoto}>{uploadingPhoto ? <ActivityIndicator size="small" color={colors.violet} /> : <Camera size={19} color={colors.violet} />}<Text style={s.addText}>{uploadingPhoto ? 'Ekleniyor…' : 'Fotoğraf ekle'}</Text></Pressable> : null}</View>{uploadingPhoto?<Text accessibilityLiveRegion="polite" style={s.photoUploadStatus}>{photoUploadStage}</Text>:null}{photoUploadError?<Text accessibilityLiveRegion="assertive" style={s.photoUploadError}>{photoUploadError}</Text>:null}</View>
      <Button title={visible.isPending ? 'Güncelleniyor…' : p.isProfileVisible ? 'Keşifte görünüyorsun' : 'Profilin gizli'} variant={p.isProfileVisible ? 'soft' : 'outline'} onPress={() => visible.mutate(!p.isProfileVisible)} style={s.visibility} icon={<Check size={16} color={colors.violet} />} />
    </Card>

    <SectionLabel>Benim dünyam</SectionLabel>
    <Card style={s.interestCard}><View style={s.sectionTitleRow}><Sparkles size={17} color={colors.violet} /><Text style={s.cardHeading}>İlgi alanları</Text><Pressable onPress={() => router.push('/settings')}><Text style={s.editLink}>Düzenle</Text></Pressable></View>{selectedInterestNames.length ? <View style={s.chips}>{selectedInterestNames.slice(0, 8).map(name => <View key={name} style={s.chip}><Text style={s.chipText}>{name}</Text></View>)}</View> : <Text style={s.muted}>İlgi alanlarını seç, ortak noktalarını keşfet.</Text>}</Card>

    <SectionLabel>Hesap</SectionLabel>
    <View style={s.rows}>
      <Row icon={<Crown color="#C58B26" size={19} />} title="Premium" detail={premium.data?.isPremium ? 'Premium üyeliğin aktif' : 'Üyelik avantajlarını keşfet'} onPress={() => router.push('/premium')} />
      <Row icon={<Sparkles color={colors.violet} size={19} />} title="Bağlantı istekleri" detail="Eşleşme ve mesaj isteklerini yönet" onPress={() => router.push('/requests')} />
      <Row icon={<Shield color={colors.violet} size={19} />} title="Ayarlar ve gizlilik" detail="Profilini ve tercihlerini düzenle" onPress={() => router.push('/settings')} />
      {admin ? <Row icon={<Shield color={colors.coral} size={19} />} title="Moderasyon paneli" detail="Rapor kuyruğunu incele" onPress={() => router.push('/admin')} /> : null}
      <Row icon={<LogOut color={colors.red} size={19} />} title="Oturumu kapat" detail="Bu cihazdan güvenli çıkış" onPress={() => Alert.alert('Çıkış yapılsın mı?', 'Oturumun bu cihazdan kapatılacak.', [{ text: 'Vazgeç', style: 'cancel' }, { text: 'Çıkış yap', style: 'destructive', onPress: () => signOut() }])} />
    </View>
  </Screen>;
}

function Row({ icon, title, detail, onPress }: { icon: React.ReactNode; title: string; detail: string; onPress: () => void }) {
  return <Pressable onPress={onPress} style={s.row}>{icon}<View style={{ flex: 1 }}><Text style={s.rowTitle}>{title}</Text><Text style={s.rowDetail}>{detail}</Text></View><ChevronRight size={18} color={colors.faint} /></Pressable>;
}

const s = StyleSheet.create({
  screen: { paddingHorizontal: 22, paddingTop: 8 }, topLine: { flexDirection: 'row', justifyContent: 'space-between', alignItems: 'center', marginBottom: 16 }, eyebrow: { color: colors.violet, fontSize: 9, fontWeight: '900', letterSpacing: 1.4 }, pageTitle: { color: colors.ink, fontSize: 28, fontWeight: '900', letterSpacing: -.8, marginTop: 4 }, settingsButton: { height: 44, width: 44, borderRadius: 16, backgroundColor: colors.white, borderWidth: 1, borderColor: colors.line, alignItems: 'center', justifyContent: 'center' },
  hero: { padding: 17 }, heroTop: { flexDirection: 'row', alignItems: 'center', gap: 13 }, avatarRing: { padding: 3, borderRadius: 45, borderWidth: 2, borderColor: colors.line, position: 'relative' }, avatarRingActive: { borderColor: colors.coral }, storyDot: { position: 'absolute', right: -2, bottom: 0, height: 19, minWidth: 19, borderRadius: 10, backgroundColor: colors.violet, borderWidth: 2, borderColor: colors.white, alignItems: 'center', justifyContent: 'center', paddingHorizontal: 4 }, storyDotText: { color: colors.white, fontSize: 9, fontWeight: '900' }, identity: { flex: 1 }, name: { color: colors.ink, fontSize: 18, fontWeight: '900' }, handle: { color: colors.muted, fontSize: 11, marginTop: 4 }, location: { flexDirection: 'row', alignItems: 'center', gap: 4, marginTop: 6 }, locationText: { color: colors.violet, fontSize: 11, fontWeight: '700' }, editButton: { width: 36, height: 36, borderRadius: 13, backgroundColor: colors.paleViolet, alignItems: 'center', justifyContent: 'center' }, bio: { color: colors.ink, fontSize: 12, lineHeight: 18, marginTop: 17 },
  progressHead: { flexDirection: 'row', justifyContent: 'space-between', marginTop: 17, marginBottom: 7 }, progressTitle: { color: colors.ink, fontSize: 10, fontWeight: '800' }, progressValue: { color: colors.violet, fontSize: 10, fontWeight: '900' }, progressTrack: { height: 6, borderRadius: 4, backgroundColor: '#EEEAF8', overflow: 'hidden' }, progressFill: { height: 6, backgroundColor: colors.violet, borderRadius: 4 }, photoSection: { marginTop: 9 }, photoUploadStatus: { color: colors.violet, fontSize: 11, fontWeight: '700', marginTop: 8 }, photoUploadError: { color: colors.red, fontSize: 11, fontWeight: '700', marginTop: 8 }, photoCount: { color: colors.muted, fontSize: 11, fontWeight: '600' }, photoGrid: { flexDirection: 'row', flexWrap: 'wrap', gap: 9 }, photoTile: { width: 76, height: 84, borderRadius: 15, overflow: 'hidden', backgroundColor: '#E7E1FB' }, primaryBadge: { position: 'absolute', left: 5, bottom: 5, flexDirection: 'row', alignItems: 'center', gap: 3, backgroundColor: '#33265DCC', paddingHorizontal: 6, paddingVertical: 4, borderRadius: 8 }, badgeText: { color: colors.white, fontSize: 8, fontWeight: '900' }, photoMenu: { position: 'absolute', right: 5, top: 5, borderRadius: 8, backgroundColor: '#33265DCC', width: 22, height: 22, alignItems: 'center', justifyContent: 'center' }, photoMenuText: { color: colors.white, fontSize: 10, fontWeight: '900' }, addTile: { width: 76, height: 84, borderRadius: 15, borderWidth: 1, borderColor: '#DCD5F3', borderStyle: 'dashed', backgroundColor: '#FAF9FE', alignItems: 'center', justifyContent: 'center', gap: 7 }, addText: { color: colors.violet, fontSize: 9, fontWeight: '800' }, disabled: { opacity: .6 }, visibility: { marginTop: 17 },
  interestCard: { padding: 15 }, sectionTitleRow: { flexDirection: 'row', alignItems: 'center', gap: 8, marginBottom: 12 }, cardHeading: { color: colors.ink, fontSize: 13, fontWeight: '900', flex: 1 }, editLink: { color: colors.violet, fontSize: 11, fontWeight: '800' }, chips: { flexDirection: 'row', flexWrap: 'wrap', gap: 7 }, chip: { backgroundColor: colors.paleViolet, borderRadius: 14, paddingVertical: 7, paddingHorizontal: 10 }, chipText: { color: colors.violet, fontSize: 10, fontWeight: '700' }, muted: { color: colors.muted, fontSize: 11, lineHeight: 16 }, heading: { color: colors.ink, fontSize: 17, fontWeight: '900' }, gap: { marginTop: 14 }, rows: { marginTop: -7, marginBottom: 22 }, row: { minHeight: 70, flexDirection: 'row', alignItems: 'center', gap: 13, borderBottomWidth: 1, borderBottomColor: colors.line }, rowTitle: { fontSize: 13, color: colors.ink, fontWeight: '800' }, rowDetail: { fontSize: 10, color: colors.muted, marginTop: 4 },
});
