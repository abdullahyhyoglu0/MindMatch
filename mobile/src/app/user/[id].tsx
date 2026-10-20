import { useMemo, useState } from 'react';
import { Alert, Pressable, StyleSheet, Text, TextInput, View } from 'react-native';
import { router, useLocalSearchParams } from 'expo-router';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { ArrowLeft, Flag, Heart, MessageCircle, Shield, UserRound } from 'lucide-react-native';
import { blockApi, chatApi, discoveryApi, interestApi, profileApi } from '../../api/mindmatch';
import { Avatar, Button, Card, Loading, ProtectedImage, Screen } from '../../components/ui';
import { colors } from '../../theme/tokens';

export default function PublicProfileScreen() {
  const { id } = useLocalSearchParams<{ id: string }>();
  const qc = useQueryClient();
  const [firstMessage, setFirstMessage] = useState('');
  const profile = useQuery({ queryKey: ['profile', id], queryFn: () => profileApi.get(id), enabled: Boolean(id), retry: false });
  const photos = useQuery({ queryKey: ['profile-photos', id], queryFn: () => profileApi.photos(id), enabled: Boolean(id && profile.data?.isProfileVisible) });
  const interests = useQuery({ queryKey: ['interests'], queryFn: interestApi.list });
  const conversations = useQuery({ queryKey: ['conversations'], queryFn: chatApi.conversations });
  const existing = conversations.data?.find(conversation => conversation.otherUserId === id);
  const tags = useMemo(() => (interests.data ?? []).filter(interest => profile.data?.interestIds.includes(interest.id)), [interests.data, profile.data?.interestIds]);
  const sendMatch = useMutation({ mutationFn: () => discoveryApi.match(id), onSuccess: () => Alert.alert('İstek gönderildi', 'Karşı taraf isteğini bildirimlerinde görecek.'), onError: error => Alert.alert('İstek gönderilemedi', error.message) });
  const sendMessageRequest = useMutation({
    mutationFn: () => chatApi.sendRequest(id, firstMessage.trim()),
    onSuccess: () => { setFirstMessage(''); Alert.alert('Mesaj isteği gönderildi', 'Kabul edildiğinde sohbet açılacak.'); },
    onError: error => Alert.alert('Mesaj gönderilemedi', error.message),
  });
  const block = useMutation({
    mutationFn: () => blockApi.block(id),
    onSuccess: () => { qc.invalidateQueries(); router.back(); },
    onError: error => Alert.alert('Engellenemedi', error.message),
  });

  if (profile.isLoading) return <Screen><Loading label="Profil açılıyor" /></Screen>;
  if (profile.isError || !profile.data) return <Screen style={styles.screen}>
    <Pressable accessibilityRole="button" onPress={() => router.back()} style={styles.back}><ArrowLeft size={19} color={colors.ink} /></Pressable>
    <Card style={styles.private}><Shield color={colors.violet} size={27} /><Text style={styles.title}>Profil görüntülenemiyor</Text><Text style={styles.copy}>Bu profil gizli, kaldırılmış veya sana kapalı olabilir.</Text></Card>
  </Screen>;
  const person = profile.data;
  const primary = photos.data?.find(photo => photo.isPrimary) ?? photos.data?.[0];

  function openChat() {
    if (!existing) return;
    router.push({ pathname: '/conversation/[id]', params: { id: existing.id, userId: id, name: person.displayName } });
  }

  return <Screen scroll style={styles.screen}>
    <View style={styles.header}><Pressable accessibilityRole="button" onPress={() => router.back()} style={styles.back}><ArrowLeft size={19} color={colors.ink} /></Pressable><Text style={styles.eyebrow}>PROFİL</Text><Pressable accessibilityRole="button" accessibilityLabel="Profili bildir" onPress={() => router.push({ pathname: '/report', params: { userId: id } })} style={styles.back}><Flag size={17} color={colors.coral} /></Pressable></View>
    <Card style={styles.hero}>
      <View style={styles.identity}><Avatar name={person.displayName} size={80} mediaAssetId={primary?.mediaAssetId} /><View style={styles.personCopy}><Text style={styles.name}>{person.displayName}</Text><Text style={styles.handle}>@{person.nickname}</Text>{person.city ? <Text style={styles.city}>{person.city}{person.age ? ` · ${person.age}` : ''}</Text> : person.age ? <Text style={styles.city}>{person.age}</Text> : null}</View></View>
      {person.bio ? <Text style={styles.bio}>{person.bio}</Text> : <Text style={styles.bioMuted}>Henüz bir biyografi eklenmemiş.</Text>}
      {photos.isLoading ? <Text style={styles.meta}>Fotoğraflar yükleniyor…</Text> : photos.data?.length ? <View style={styles.photoGrid}>{photos.data.map(photo => <View key={photo.id} style={styles.photo}>{photo.mediaAssetId ? <ProtectedImage mediaAssetId={photo.mediaAssetId} style={StyleSheet.absoluteFill} /> : null}</View>)}</View> : <Text style={styles.meta}>Henüz profil fotoğrafı yok.</Text>}
    </Card>
    {tags.length ? <Card style={styles.interests}><View style={styles.sectionTitle}><UserRound size={17} color={colors.violet} /><Text style={styles.sectionText}>İlgi alanları</Text></View><View style={styles.tags}>{tags.map(tag => <View key={tag.id} style={styles.tag}><Text style={styles.tagText}>{tag.name}</Text></View>)}</View></Card> : null}
    {existing ? <Button title="Sohbete geç" icon={<MessageCircle size={17} color={colors.white} />} onPress={openChat} style={styles.cta} /> : <>
      <Button title="Eşleşme isteği gönder" icon={<Heart size={17} color={colors.white} />} onPress={() => sendMatch.mutate()} loading={sendMatch.isPending} style={styles.cta} />
      <Card style={styles.messageCard}><Text style={styles.sectionText}>İlk mesajını gönder</Text><Text style={styles.messageHint}>Mesaj isteği ancak karşı taraf kabul ederse sohbete dönüşür.</Text><TextInput value={firstMessage} onChangeText={setFirstMessage} placeholder="Kısa bir merhaba yaz…" placeholderTextColor={colors.faint} maxLength={500} multiline style={styles.input} /><Button title="Mesaj isteği gönder" onPress={() => sendMessageRequest.mutate()} loading={sendMessageRequest.isPending} disabled={!firstMessage.trim()} variant="soft" style={styles.messageButton} /></Card>
    </>}
    <Pressable accessibilityRole="button" onPress={() => Alert.alert('Kişiyi engelle', 'Bu kişi sana ulaşamaz ve profilini göremez. Emin misin?', [{ text: 'Vazgeç', style: 'cancel' }, { text: 'Engelle', style: 'destructive', onPress: () => block.mutate() }])} style={styles.block}><Shield size={16} color={colors.red} /><Text style={styles.blockText}>Kişiyi engelle</Text></Pressable>
  </Screen>;
}

const styles = StyleSheet.create({
  screen: { paddingHorizontal: 20, paddingTop: 12 }, header: { flexDirection: 'row', justifyContent: 'space-between', alignItems: 'center', marginBottom: 15 }, back: { width: 40, height: 40, borderRadius: 14, backgroundColor: colors.white, borderWidth: 1, borderColor: colors.line, alignItems: 'center', justifyContent: 'center' }, eyebrow: { fontSize: 9, letterSpacing: 1.5, fontWeight: '900', color: colors.violet },
  hero: { padding: 17 }, identity: { flexDirection: 'row', alignItems: 'center', gap: 14 }, personCopy: { flex: 1 }, name: { fontSize: 20, color: colors.ink, fontWeight: '900' }, handle: { fontSize: 11, color: colors.muted, marginTop: 4 }, city: { fontSize: 11, color: colors.violet, marginTop: 6 }, bio: { fontSize: 13, lineHeight: 20, color: colors.ink, marginTop: 17 }, bioMuted: { fontSize: 12, color: colors.muted, marginTop: 17 }, meta: { fontSize: 10, color: colors.muted, marginTop: 15 }, photoGrid: { flexDirection: 'row', flexWrap: 'wrap', gap: 9, marginTop: 15 }, photo: { width: 86, height: 100, overflow: 'hidden', borderRadius: 15, backgroundColor: colors.paleViolet },
  interests: { padding: 15, marginTop: 12 }, sectionTitle: { flexDirection: 'row', alignItems: 'center', gap: 8 }, sectionText: { fontSize: 13, fontWeight: '900', color: colors.ink }, tags: { flexDirection: 'row', flexWrap: 'wrap', gap: 7, marginTop: 12 }, tag: { borderRadius: 16, paddingHorizontal: 11, paddingVertical: 8, backgroundColor: colors.paleViolet }, tagText: { fontSize: 10, color: colors.violet, fontWeight: '800' }, cta: { marginTop: 14 }, messageCard: { padding: 15, marginTop: 11 }, messageHint: { fontSize: 10, color: colors.muted, marginTop: 5 }, input: { minHeight: 74, marginTop: 10, padding: 12, borderRadius: 14, borderWidth: 1, borderColor: colors.line, backgroundColor: colors.canvas, textAlignVertical: 'top', fontSize: 13, color: colors.ink }, messageButton: { marginTop: 10 }, block: { flexDirection: 'row', gap: 8, alignItems: 'center', justifyContent: 'center', paddingVertical: 20 }, blockText: { color: colors.red, fontSize: 11, fontWeight: '800' }, private: { marginTop: 70, padding: 22, alignItems: 'center', gap: 10 }, title: { color: colors.ink, fontSize: 16, fontWeight: '900' }, copy: { color: colors.muted, fontSize: 12, textAlign: 'center', lineHeight: 17 },
});
