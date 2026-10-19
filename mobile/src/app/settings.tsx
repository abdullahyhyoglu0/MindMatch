import { useState } from 'react';
import { Alert, Pressable, StyleSheet, Switch, Text, View } from 'react-native';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { router } from 'expo-router';
import { ArrowLeft, LockKeyhole, ShieldCheck, UserRound } from 'lucide-react-native';
import { accountApi, profileApi } from '../api/mindmatch';
import { Button, Card, Field, Loading, Screen } from '../components/ui';
import { colors } from '../theme/tokens';
import { useSession } from '../state/session';

export default function SettingsScreen() {
  const qc = useQueryClient();
  const profile = useQuery({ queryKey: ['my-profile'], queryFn: profileApi.mine });
  const [name, setName] = useState<string | null>(null);
  const [nickname, setNickname] = useState<string | null>(null);
  const [city, setCity] = useState<string | null>(null);
  const [bio, setBio] = useState<string | null>(null);
  const [allowMessages, setAllowMessages] = useState<boolean | null>(null);
  const [allowCalls, setAllowCalls] = useState<boolean | null>(null);
  const [online, setOnline] = useState<boolean | null>(null);
  const [lastSeen, setLastSeen] = useState<boolean | null>(null);
  const [readReceipts, setReadReceipts] = useState<boolean | null>(null);
  const signOut = useSession(x => x.signOut);
  const current = profile.data;

  const saveProfile = useMutation({
    mutationFn: () => profileApi.update({
      displayName: name ?? current?.displayName ?? '', nickname: nickname ?? current?.nickname ?? '',
      bio: bio ?? current?.bio ?? '', city: city ?? current?.city ?? '', birthDate: current?.birthDate,
      gender: current?.gender, genderPreference: current?.genderPreference,
      minimumMatchAge: current?.minimumMatchAge, maximumMatchAge: current?.maximumMatchAge,
    }),
    onSuccess: () => { qc.invalidateQueries({ queryKey: ['my-profile'] }); Alert.alert('Değişiklikler kaydedildi', 'Profil bilgilerin güncellendi.'); },
    onError: error => Alert.alert('Kaydedilemedi', error.message),
  });
  const savePrivacy = useMutation({
    mutationFn: () => profileApi.updatePreferences({
      allowMessageRequests: allowMessages ?? current?.allowMessageRequests ?? true,
      allowCallsFromNonMatches: allowCalls ?? current?.allowCallsFromNonMatches ?? false,
      showOnlineStatus: online ?? current?.showOnlineStatus ?? true,
      showLastSeen: lastSeen ?? current?.showLastSeen ?? true,
      showReadReceipts: readReceipts ?? current?.showReadReceipts ?? true,
      showPremiumBadge: current?.showPremiumBadge ?? true,
      premiumPriorityOptIn: current?.premiumPriorityOptIn ?? false,
    }),
    onSuccess: () => { qc.invalidateQueries({ queryKey: ['my-profile'] }); Alert.alert('Gizlilik ayarları kaydedildi'); },
    onError: error => Alert.alert('Kaydedilemedi', error.message),
  });
  const toggleVisibility = useMutation({
    mutationFn: () => profileApi.setVisibility(!current?.isProfileVisible),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['my-profile'] }),
    onError: error => Alert.alert('Görünürlük değiştirilemedi', error.message),
  });
  const freeze = useMutation({
    mutationFn: accountApi.freeze,
    onSuccess: () => { Alert.alert('Hesap donduruldu', 'Tekrar kullanmak istediğinde giriş yapabilirsin.'); signOut(); },
    onError: error => Alert.alert('İşlem tamamlanamadı', error.message),
  });

  if (profile.isLoading) return <Screen><Loading label="Ayarların yükleniyor" /></Screen>;
  if (profile.error || !current) return <Screen style={styles.page}><TopBar /><Card><Text style={styles.cardTitle}>Ayarlar açılamadı</Text><Text style={styles.muted}>{profile.error instanceof Error ? profile.error.message : 'Profil bilgilerine ulaşılamadı.'}</Text><Button title="Yeniden dene" onPress={() => profile.refetch()} style={styles.topGap} /></Card></Screen>;

  return (
    <Screen scroll style={styles.page}>
      <TopBar />
      <Text style={styles.eyebrow}>HESABIN VE KONTROLÜN</Text>
      <Text style={styles.title}>Ayarlar</Text>
      <Text style={styles.subtitle}>MindMatch deneyimini kendine göre düzenle.</Text>

      <View style={styles.sectionHeading}><UserRound size={17} color={colors.violet} /><Text style={styles.sectionTitle}>Profil bilgileri</Text></View>
      <Card style={styles.card}>
        <Text style={styles.helper}>Bağlantıların seni bu bilgilerle tanıyacak.</Text>
        <View style={styles.fields}>
          <Field label="Görünen ad" value={name ?? current.displayName} onChangeText={setName} placeholder="Adın" maxLength={60} />
          <Field label="Kullanıcı adı" value={nickname ?? current.nickname} onChangeText={setNickname} placeholder="kullaniciadi" maxLength={30} autoCapitalize="none" />
          <Field label="Şehir" value={city ?? current.city ?? ''} onChangeText={setCity} placeholder="Şehir" maxLength={80} />
          <Field label="Hakkımda" value={bio ?? current.bio ?? ''} onChangeText={setBio} placeholder="Kendinden kısaca bahset" multiline maxLength={500} style={styles.bioInput} />
        </View>
        <Button title="Profil bilgilerini kaydet" onPress={() => saveProfile.mutate()} loading={saveProfile.isPending} style={styles.topGap} />
      </Card>

      <View style={styles.sectionHeading}><ShieldCheck size={17} color={colors.violet} /><Text style={styles.sectionTitle}>Görünürlük</Text></View>
      <Card style={styles.card}>
        <SettingRow title="Keşifte görün" detail="Yeni kişilerin seni bulmasına izin ver." value={current.isProfileVisible} onChange={() => toggleVisibility.mutate()} disabled={toggleVisibility.isPending} />
        <SettingRow title="Çevrimiçi durumunu göster" detail="Aktif olduğun bilgisini paylaş." value={online ?? current.showOnlineStatus} onChange={setOnline} />
        <SettingRow title="Son görülmeyi göster" detail="Son aktif olduğun zamanı paylaş." value={lastSeen ?? current.showLastSeen} onChange={setLastSeen} />
      </Card>

      <View style={styles.sectionHeading}><LockKeyhole size={17} color={colors.violet} /><Text style={styles.sectionTitle}>Mesaj ve arama izinleri</Text></View>
      <Card style={styles.card}>
        <SettingRow title="Mesaj isteği al" detail="Eşleşmediğin kişiler sana mesaj isteği gönderebilsin." value={allowMessages ?? current.allowMessageRequests} onChange={setAllowMessages} />
        <SettingRow title="Eşleşmediğin kişilerden arama al" detail="Bu izni açarsan eşleşmediğin kişiler de arayabilir." value={allowCalls ?? current.allowCallsFromNonMatches} onChange={setAllowCalls} />
        <SettingRow title="Okundu bilgisini göster" detail="Mesajı ne zaman okuduğunu karşı tarafa bildir." value={readReceipts ?? current.showReadReceipts} onChange={setReadReceipts} />
        <Button title="İzinleri kaydet" variant="soft" onPress={() => savePrivacy.mutate()} loading={savePrivacy.isPending} style={styles.topGap} />
      </Card>

      <View style={styles.sectionHeading}><LockKeyhole size={17} color={colors.coral} /><Text style={styles.sectionTitle}>Hesap</Text></View>
      <Card style={[styles.card, styles.lastCard]}>
        <Text style={styles.cardTitle}>Hesabı geçici dondur</Text>
        <Text style={styles.helper}>Profilin ve yeni girişlerin duraklatılır. Dilediğinde tekrar etkinleştirebilirsin.</Text>
        <Button title="Hesabı dondur" variant="outline" onPress={() => Alert.alert('Hesap dondurulsun mu?', 'Hesabın geçici olarak devre dışı kalacak.', [{ text: 'Vazgeç', style: 'cancel' }, { text: 'Dondur', style: 'destructive', onPress: () => freeze.mutate() }])} loading={freeze.isPending} style={styles.topGap} />
      </Card>
    </Screen>
  );
}

function TopBar() {
  return <Pressable onPress={() => router.back()} style={styles.back}><ArrowLeft size={19} color={colors.ink} /></Pressable>;
}
function SettingRow({ title, detail, value, onChange, disabled = false }: { title: string; detail: string; value: boolean; onChange: (value: boolean) => void; disabled?: boolean }) {
  return <View style={styles.settingRow}><View style={styles.settingCopy}><Text style={styles.settingTitle}>{title}</Text><Text style={styles.settingDetail}>{detail}</Text></View><Switch value={value} onValueChange={onChange} disabled={disabled} trackColor={{ false: '#DAD7E2', true: '#A89AF4' }} thumbColor={value ? colors.violet : colors.white} /></View>;
}

const styles = StyleSheet.create({
  page: { paddingHorizontal: 22, paddingTop: 10 }, back: { width: 42, height: 42, borderRadius: 15, alignItems: 'center', justifyContent: 'center', backgroundColor: colors.white, borderWidth: 1, borderColor: colors.line, marginBottom: 20 },
  eyebrow: { color: colors.violet, fontSize: 9, fontWeight: '900', letterSpacing: 1.5 }, title: { fontSize: 31, lineHeight: 38, fontWeight: '900', letterSpacing: -1, color: colors.ink, marginTop: 5 }, subtitle: { color: colors.muted, fontSize: 12, lineHeight: 18, marginTop: 4, marginBottom: 10 },
  sectionHeading: { flexDirection: 'row', alignItems: 'center', gap: 8, marginTop: 19, marginBottom: 9 }, sectionTitle: { fontSize: 14, color: colors.ink, fontWeight: '900' }, card: { padding: 16 }, fields: { gap: 12, marginTop: 12 }, helper: { fontSize: 11, lineHeight: 16, color: colors.muted }, muted: { color: colors.muted, marginTop: 7, fontSize: 12 }, cardTitle: { fontSize: 13, color: colors.ink, fontWeight: '900' }, bioInput: { height: 84, paddingTop: 12, textAlignVertical: 'top' }, topGap: { marginTop: 14 },
  settingRow: { minHeight: 68, flexDirection: 'row', alignItems: 'center', gap: 8, borderBottomWidth: 1, borderBottomColor: colors.line, paddingVertical: 8 }, settingCopy: { flex: 1 }, settingTitle: { fontSize: 11, color: colors.ink, fontWeight: '800' }, settingDetail: { fontSize: 9, color: colors.muted, lineHeight: 13, marginTop: 3 }, lastCard: { marginBottom: 30 },
});
