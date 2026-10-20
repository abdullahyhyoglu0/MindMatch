import { useMemo, useState } from 'react';
import { Alert, Modal, Pressable, StyleSheet, Text, TextInput, View } from 'react-native';
import { router, useLocalSearchParams } from 'expo-router';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { ArrowLeft, Flag, Heart, MessageCircle, Shield, UserRound, X, Sparkles } from 'lucide-react-native';
import { blockApi, chatApi, discoveryApi, interestApi, profileApi } from '../../api/mindmatch';
import { Avatar, Button, Card, Loading, ProtectedImage, Screen } from '../../components/ui';
import { colors } from '../../theme/tokens';

export default function PublicProfileScreen() {
  const { id } = useLocalSearchParams<{ id: string }>();
  const qc = useQueryClient();
  const [firstMessage, setFirstMessage] = useState('');
  const [selectedPhotoAssetId, setSelectedPhotoAssetId] = useState<string | null>(null);

  const profile = useQuery({ queryKey: ['profile', id], queryFn: () => profileApi.get(id), enabled: Boolean(id), retry: false });
  const photos = useQuery({ queryKey: ['profile-photos', id], queryFn: () => profileApi.photos(id), enabled: Boolean(id && profile.data?.isProfileVisible) });
  const interests = useQuery({ queryKey: ['interests'], queryFn: interestApi.list });
  const conversations = useQuery({ queryKey: ['conversations'], queryFn: chatApi.conversations });

  const existing = conversations.data?.find(conversation => conversation.otherUserId === id);
  const tags = useMemo(() => (interests.data ?? []).filter(interest => profile.data?.interestIds.includes(interest.id)), [interests.data, profile.data?.interestIds]);

  const sendMatch = useMutation({
    mutationFn: () => discoveryApi.match(id),
    onSuccess: () => Alert.alert('İstek gönderildi', 'Karşı taraf isteğini bildirimlerinde görecek.'),
    onError: error => Alert.alert('İstek gönderilemedi', error.message),
  });

  const sendMessageRequest = useMutation({
    mutationFn: () => chatApi.sendRequest(id, firstMessage.trim()),
    onSuccess: () => {
      setFirstMessage('');
      Alert.alert('Mesaj isteği gönderildi', 'Kabul edildiğinde sohbet açılacak.');
    },
    onError: error => Alert.alert('Mesaj gönderilemedi', error.message),
  });

  const block = useMutation({
    mutationFn: () => blockApi.block(id),
    onSuccess: () => {
      qc.invalidateQueries();
      router.back();
    },
    onError: error => Alert.alert('Engellenemedi', error.message),
  });

  if (profile.isLoading) return <Screen><Loading label="Profil açılıyor" /></Screen>;

  if (profile.isError || !profile.data) {
    return (
      <Screen style={styles.screen}>
        <Pressable accessibilityRole="button" onPress={() => router.back()} style={styles.back}>
          <ArrowLeft size={19} color={colors.ink} />
        </Pressable>
        <Card style={styles.private}>
          <Shield color={colors.violet} size={30} />
          <Text style={styles.title}>Profil görüntülenemiyor</Text>
          <Text style={styles.copy}>Bu profil gizli, kaldırılmış veya sana kapalı olabilir.</Text>
        </Card>
      </Screen>
    );
  }

  const person = profile.data;
  const primary = photos.data?.find(photo => photo.isPrimary) ?? photos.data?.[0];

  function openChat() {
    if (!existing) return;
    router.push({ pathname: '/conversation/[id]', params: { id: existing.id, userId: id, name: person.displayName } });
  }

  return (
    <Screen scroll style={styles.screen}>
      <View style={styles.header}>
        <Pressable accessibilityRole="button" onPress={() => router.back()} style={styles.back}>
          <ArrowLeft size={19} color={colors.ink} />
        </Pressable>
        <View style={styles.headerTitleWrap}>
          <Text style={styles.eyebrow}>PROFİL DETAYI</Text>
          <Text style={styles.headerTitle} numberOfLines={1}>{person.displayName}</Text>
        </View>
        <Pressable
          accessibilityRole="button"
          accessibilityLabel="Profili bildir"
          onPress={() => router.push({ pathname: '/report', params: { userId: id } })}
          style={styles.back}
        >
          <Flag size={17} color={colors.coral} />
        </Pressable>
      </View>

      <Card style={styles.hero}>
        <View style={styles.identity}>
          <Pressable onPress={() => primary?.mediaAssetId && setSelectedPhotoAssetId(primary.mediaAssetId)}>
            <Avatar name={person.displayName} size={84} mediaAssetId={primary?.mediaAssetId} />
          </Pressable>
          <View style={styles.personCopy}>
            <View style={styles.nameRow}>
              <Text style={styles.name}>{person.displayName}</Text>
              {person.isVerified ? <Sparkles size={16} color={colors.violet} /> : null}
            </View>
            <Text style={styles.handle}>@{person.nickname}</Text>
            {person.city ? (
              <Text style={styles.city}>{person.city}{person.age ? ` · ${person.age} yaş` : ''}</Text>
            ) : person.age ? (
              <Text style={styles.city}>{person.age} yaş</Text>
            ) : null}
          </View>
        </View>

        {person.bio ? (
          <Text style={styles.bio}>{person.bio}</Text>
        ) : (
          <Text style={styles.bioMuted}>Henüz bir biyografi eklenmemiş.</Text>
        )}

        {photos.isLoading ? (
          <Text style={styles.meta}>Fotoğraflar yükleniyor…</Text>
        ) : photos.data?.length ? (
          <View style={styles.photoSection}>
            <Text style={styles.photoSectionTitle}>Fotoğraf Galerisi</Text>
            <View style={styles.photoGrid}>
              {photos.data.map(photo => (
                <Pressable
                  key={photo.id}
                  style={styles.photo}
                  onPress={() => photo.mediaAssetId && setSelectedPhotoAssetId(photo.mediaAssetId)}
                >
                  {photo.mediaAssetId ? (
                    <ProtectedImage mediaAssetId={photo.mediaAssetId} style={StyleSheet.absoluteFill} />
                  ) : null}
                </Pressable>
              ))}
            </View>
          </View>
        ) : null}
      </Card>

      {tags.length ? (
        <Card style={styles.interests}>
          <View style={styles.sectionTitle}>
            <UserRound size={17} color={colors.violet} />
            <Text style={styles.sectionText}>İlgi Alanları</Text>
          </View>
          <View style={styles.tags}>
            {tags.map(tag => (
              <View key={tag.id} style={styles.tag}>
                <Text style={styles.tagText}>{tag.name}</Text>
              </View>
            ))}
          </View>
        </Card>
      ) : null}

      {existing ? (
        <Button
          title="Sohbete geç"
          icon={<MessageCircle size={18} color={colors.white} />}
          onPress={openChat}
          style={styles.cta}
        />
      ) : (
        <>
          <Button
            title="Eşleşme isteği gönder"
            icon={<Heart size={18} color={colors.white} />}
            onPress={() => sendMatch.mutate()}
            loading={sendMatch.isPending}
            style={styles.cta}
          />
          <Card style={styles.messageCard}>
            <Text style={styles.sectionText}>İlk mesajını gönder</Text>
            <Text style={styles.messageHint}>Mesaj isteği ancak karşı taraf kabul ederse sohbete dönüşür.</Text>
            <TextInput
              value={firstMessage}
              onChangeText={setFirstMessage}
              placeholder="Kısa bir merhaba yaz…"
              placeholderTextColor={colors.faint}
              maxLength={500}
              multiline
              style={styles.input}
            />
            <Button
              title="Mesaj isteği gönder"
              onPress={() => sendMessageRequest.mutate()}
              loading={sendMessageRequest.isPending}
              disabled={!firstMessage.trim()}
              variant="soft"
              style={styles.messageButton}
            />
          </Card>
        </>
      )}

      <Pressable
        accessibilityRole="button"
        onPress={() =>
          Alert.alert('Kişiyi engelle', 'Bu kişi sana ulaşamaz ve profilini göremez. Emin misin?', [
            { text: 'Vazgeç', style: 'cancel' },
            { text: 'Engelle', style: 'destructive', onPress: () => block.mutate() },
          ])
        }
        style={styles.block}
      >
        <Shield size={16} color={colors.red} />
        <Text style={styles.blockText}>Kişiyi engelle</Text>
      </Pressable>

      {/* Fullscreen Photo Lightbox Modal */}
      <Modal visible={!!selectedPhotoAssetId} transparent animationType="fade" onRequestClose={() => setSelectedPhotoAssetId(null)}>
        <View style={styles.lightboxContainer}>
          <Pressable style={styles.lightboxClose} onPress={() => setSelectedPhotoAssetId(null)}>
            <X size={24} color="#FFF" />
          </Pressable>
          {selectedPhotoAssetId ? (
            <View style={styles.lightboxImageWrap}>
              <ProtectedImage mediaAssetId={selectedPhotoAssetId} style={styles.lightboxImage} />
            </View>
          ) : null}
        </View>
      </Modal>
    </Screen>
  );
}

const styles = StyleSheet.create({
  screen: { paddingHorizontal: 16, paddingTop: 10, paddingBottom: 40 },
  header: { flexDirection: 'row', justifyContent: 'space-between', alignItems: 'center', marginBottom: 14 },
  headerTitleWrap: { alignItems: 'center' },
  headerTitle: { fontSize: 14, fontWeight: '800', color: colors.ink, marginTop: 2 },
  back: { width: 42, height: 42, borderRadius: 14, backgroundColor: colors.white, borderWidth: 1, borderColor: colors.line, alignItems: 'center', justifyContent: 'center' },
  eyebrow: { fontSize: 9, letterSpacing: 1.5, fontWeight: '900', color: colors.violet },
  hero: { padding: 18, borderRadius: 20 },
  identity: { flexDirection: 'row', alignItems: 'center', gap: 16 },
  personCopy: { flex: 1 },
  nameRow: { flexDirection: 'row', alignItems: 'center', gap: 6 },
  name: { fontSize: 22, color: colors.ink, fontWeight: '900', letterSpacing: -0.5 },
  handle: { fontSize: 12, color: colors.muted, marginTop: 3, fontWeight: '600' },
  city: { fontSize: 12, color: colors.violet, marginTop: 6, fontWeight: '700' },
  bio: { fontSize: 14, lineHeight: 21, color: colors.ink, marginTop: 16 },
  bioMuted: { fontSize: 13, color: colors.muted, marginTop: 16, fontStyle: 'italic' },
  meta: { fontSize: 11, color: colors.muted, marginTop: 15 },
  photoSection: { marginTop: 18 },
  photoSectionTitle: { fontSize: 12, fontWeight: '800', color: colors.ink, marginBottom: 10, letterSpacing: 0.3 },
  photoGrid: { flexDirection: 'row', flexWrap: 'wrap', gap: 10 },
  photo: { width: '31%', aspectRatio: 1, overflow: 'hidden', borderRadius: 14, backgroundColor: colors.paleViolet, borderWidth: 1, borderColor: colors.line },
  interests: { padding: 16, marginTop: 12, borderRadius: 20 },
  sectionTitle: { flexDirection: 'row', alignItems: 'center', gap: 8 },
  sectionText: { fontSize: 14, fontWeight: '800', color: colors.ink },
  tags: { flexDirection: 'row', flexWrap: 'wrap', gap: 8, marginTop: 12 },
  tag: { borderRadius: 14, paddingHorizontal: 12, paddingVertical: 7, backgroundColor: colors.paleViolet, borderWidth: 1, borderColor: '#EDE8FF' },
  tagText: { fontSize: 11, color: colors.violet, fontWeight: '700' },
  cta: { marginTop: 14 },
  messageCard: { padding: 16, marginTop: 12, borderRadius: 20 },
  messageHint: { fontSize: 11, color: colors.muted, marginTop: 4, lineHeight: 16 },
  input: { minHeight: 80, marginTop: 12, padding: 14, borderRadius: 14, borderWidth: 1, borderColor: colors.line, backgroundColor: colors.canvas, textAlignVertical: 'top', fontSize: 14, color: colors.ink },
  messageButton: { marginTop: 12 },
  block: { flexDirection: 'row', gap: 8, alignItems: 'center', justifyContent: 'center', paddingVertical: 24 },
  blockText: { color: colors.red, fontSize: 12, fontWeight: '800' },
  private: { marginTop: 70, padding: 26, alignItems: 'center', gap: 12, borderRadius: 20 },
  title: { color: colors.ink, fontSize: 17, fontWeight: '900' },
  copy: { color: colors.muted, fontSize: 13, textAlign: 'center', lineHeight: 19 },
  lightboxContainer: { flex: 1, backgroundColor: 'rgba(0,0,0,0.92)', justifyContent: 'center', alignItems: 'center' },
  lightboxClose: { position: 'absolute', top: 54, right: 20, zIndex: 10, width: 44, height: 44, borderRadius: 22, backgroundColor: 'rgba(255,255,255,0.2)', alignItems: 'center', justifyContent: 'center' },
  lightboxImageWrap: { width: '92%', height: '75%', borderRadius: 16, overflow: 'hidden' },
  lightboxImage: { width: '100%', height: '100%' },
});
