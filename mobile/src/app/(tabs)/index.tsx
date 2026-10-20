import { useMemo, useState } from 'react';
import {
  ActivityIndicator,
  Alert,
  Dimensions,
  Modal,
  Pressable,
  ScrollView,
  StyleSheet,
  Text,
  TextInput,
  View,
} from 'react-native';
import { router } from 'expo-router';
import { useMutation, useQueries, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  Bell,
  ChevronRight,
  Crown,
  Filter,
  Lock,
  MapPin,
  MessageCircle,
  RotateCw,
  Search,
  Send,
  SlidersHorizontal,
  Sparkles,
  UserCheck,
  X,
} from 'lucide-react-native';
import { LinearGradient } from 'expo-linear-gradient';
import * as Haptics from 'expo-haptics';
import { chatApi, discoveryApi, interestApi, premiumApi, profileApi, storyApi } from '../../api/mindmatch';
import type { Profile, Story } from '../../api/types';
import { useSession } from '../../state/session';
import { Avatar, Button, Card, Empty, Loading, ProtectedImage, Screen } from '../../components/ui';
import { colors } from '../../theme/tokens';

const POPULAR_CITIES = ['Tümü', 'İstanbul', 'Ankara', 'İzmir', 'Antalya', 'Bursa', 'Eskişehir'];

export default function DiscoverScreen() {
  const user = useSession(s => s.user);
  const qc = useQueryClient();

  const [searchQuery, setSearchQuery] = useState('');
  const [selectedCity, setSelectedCity] = useState('Tümü');
  const [filterModalVisible, setFilterModalVisible] = useState(false);
  const [minAge, setMinAge] = useState('18');
  const [maxAge, setMaxAge] = useState('35');
  const [genderFilter, setGenderFilter] = useState<'all' | 'female' | 'male'>('all');

  // Quick message modal for new chats
  const [activeNewChatTarget, setActiveNewChatTarget] = useState<Profile | null>(null);
  const [firstMessageText, setFirstMessageText] = useState('Merhaba! 👋');

  // Premium status check
  const premiumQuery = useQuery({ queryKey: ['premium'], queryFn: premiumApi.mine });
  const isPremium = Boolean(premiumQuery.data?.isPremium);

  // Fetch public profiles directly (No mood blocker)
  const {
    data: discovery,
    isLoading,
    isRefetching,
    refetch,
  } = useQuery({
    queryKey: ['discovery', selectedCity],
    queryFn: () => discoveryApi.list(1, 50, selectedCity === 'Tümü' ? undefined : selectedCity),
    refetchInterval: 60_000,
  });

  // Fetch active stories to identify who has stories
  const storiesQuery = useQuery({
    queryKey: ['stories'],
    queryFn: storyApi.feed,
    refetchInterval: 20_000,
  });

  const storiesByUser = useMemo(() => {
    const map = new Map<string, Story[]>();
    for (const story of storiesQuery.data ?? []) {
      const list = map.get(story.userId) || [];
      list.push(story);
      map.set(story.userId, list);
    }
    return map;
  }, [storiesQuery.data]);

  // Existing conversations map
  const conversationsQuery = useQuery({
    queryKey: ['conversations'],
    queryFn: chatApi.conversations,
  });

  const convMap = useMemo(() => {
    const map = new Map<string, string>();
    for (const conv of conversationsQuery.data ?? []) {
      map.set(conv.otherUserId, conv.id);
    }
    return map;
  }, [conversationsQuery.data]);

  // Interests map
  const interestsQuery = useQuery({ queryKey: ['interests'], queryFn: interestApi.list });
  const interestNames = useMemo(
    () => new Map((interestsQuery.data ?? []).map(i => [i.id, i.name])),
    [interestsQuery.data]
  );

  // Filter profiles based on Search and Premium age/gender filters
  const filteredProfiles = useMemo(() => {
    let list = discovery?.items ?? [];

    if (searchQuery.trim()) {
      const q = searchQuery.toLowerCase().trim();
      list = list.filter(
        p =>
          p.displayName.toLowerCase().includes(q) ||
          p.nickname?.toLowerCase().includes(q) ||
          p.city?.toLowerCase().includes(q) ||
          p.bio?.toLowerCase().includes(q)
      );
    }

    if (isPremium) {
      const min = parseInt(minAge, 10);
      const max = parseInt(maxAge, 10);
      if (!isNaN(min)) list = list.filter(p => !p.age || p.age >= min);
      if (!isNaN(max)) list = list.filter(p => !p.age || p.age <= max);
      if (genderFilter === 'female') list = list.filter(p => p.gender === 1);
      if (genderFilter === 'male') list = list.filter(p => p.gender === 2);
    }

    return list;
  }, [discovery?.items, searchQuery, isPremium, minAge, maxAge, genderFilter]);

  // Fetch primary profile photos for each person to show real photos instead of "D" letter
  const personPhotoQueries = useQueries({
    queries: (filteredProfiles ?? []).map(p => ({
      queryKey: ['profile-photos', p.userId],
      queryFn: () => profileApi.photos(p.userId),
      staleTime: 60_000,
    })),
  });

  const primaryPhotoMap = useMemo(() => {
    const map = new Map<string, string>();
    filteredProfiles.forEach((p, idx) => {
      const photos = personPhotoQueries[idx]?.data;
      const primary = photos?.find(x => x.isPrimary) ?? photos?.[0];
      if (primary?.mediaAssetId) {
        map.set(p.userId, primary.mediaAssetId);
      }
    });
    return map;
  }, [filteredProfiles, personPhotoQueries]);

  // Handle sending initial message request to start a conversation
  const startConversation = useMutation({
    mutationFn: async ({ recipientId, text }: { recipientId: string; text: string }) => {
      return chatApi.sendRequest(recipientId, text);
    },
    onSuccess: (req, variables) => {
      Haptics.notificationAsync(Haptics.NotificationFeedbackType.Success).catch(() => {});
      const target = activeNewChatTarget;
      setActiveNewChatTarget(null);
      qc.invalidateQueries({ queryKey: ['conversations'] });
      if (req.conversationId) {
        router.push({
          pathname: '/conversation/[id]',
          params: {
            id: req.conversationId,
            userId: variables.recipientId,
            name: target?.displayName || 'Kullanıcı',
          },
        });
      } else {
        Alert.alert('Mesaj İsteği Gönderildi', 'Kullanıcı onayladığında sohbet aktif olacak.');
      }
    },
    onError: (error: any) => {
      Alert.alert('Sohbet Başlatılamadı', error instanceof Error ? error.message : 'Lütfen tekrar dene.');
    },
  });

  // Tapping the card body (to the right of the avatar): opens chat screen!
  function handleCardBodyPress(person: Profile) {
    Haptics.selectionAsync().catch(() => {});
    const existingConvId = convMap.get(person.userId);
    if (existingConvId) {
      // Existing conversation -> opens directly!
      router.push({
        pathname: '/conversation/[id]',
        params: { id: existingConvId, userId: person.userId, name: person.displayName },
      });
    } else {
      // No conversation yet -> opens quick message dialog to start chat!
      setActiveNewChatTarget(person);
      setFirstMessageText('Merhaba! 👋');
    }
  }

  // Tapping the Avatar: if has stories -> opens story; if not -> opens profile!
  function handleAvatarPress(person: Profile) {
    Haptics.selectionAsync().catch(() => {});
    const userStories = storiesByUser.get(person.userId);
    if (userStories && userStories.length > 0) {
      // Has active stories -> open story viewer directly!
      router.push({
        pathname: '/story/[id]',
        params: { id: userStories[0].id, userId: person.userId },
      });
    } else {
      // No stories -> open user profile!
      router.push({ pathname: '/user/[id]', params: { id: person.userId } });
    }
  }

  return (
    <Screen scroll style={styles.screen}>
      {/* Top Header */}
      <View style={styles.topHeader}>
        <View>
          <Text style={styles.eyebrow}>MINDMATCH · KEŞFET</Text>
          <Text style={styles.title}>
            Açık <Text style={{ color: colors.violet }}>Profiller</Text>
          </Text>
        </View>

        <View style={styles.topHeaderActions}>
          <Pressable
            accessibilityRole="button"
            accessibilityLabel="Yenile"
            onPress={() => {
              Haptics.selectionAsync().catch(() => {});
              refetch();
            }}
            style={styles.iconCircle}
          >
            <RotateCw size={18} color={colors.ink} />
          </Pressable>

          <Pressable
            accessibilityRole="button"
            accessibilityLabel="Bildirimler"
            onPress={() => router.push('/(tabs)/notifications')}
            style={styles.iconCircle}
          >
            <Bell size={18} color={colors.ink} />
          </Pressable>
        </View>
      </View>

      {/* Search Bar + Filter Trigger */}
      <View style={styles.searchRow}>
        <View style={styles.searchInputWrap}>
          <Search size={18} color={colors.muted} style={{ marginLeft: 12 }} />
          <TextInput
            value={searchQuery}
            onChangeText={setSearchQuery}
            placeholder="İsim, şehir veya ilgi alanı ara…"
            placeholderTextColor={colors.muted}
            style={styles.searchInput}
          />
          {searchQuery.length > 0 && (
            <Pressable onPress={() => setSearchQuery('')} style={{ padding: 8 }}>
              <X size={16} color={colors.muted} />
            </Pressable>
          )}
        </View>

        <Pressable
          style={[styles.filterBtn, isPremium && styles.filterBtnPremium]}
          onPress={() => setFilterModalVisible(true)}
        >
          <SlidersHorizontal size={18} color={isPremium ? colors.white : colors.ink} />
        </Pressable>
      </View>

      {/* City Chips Row */}
      <ScrollView
        horizontal
        showsHorizontalScrollIndicator={false}
        contentContainerStyle={styles.cityChipsRow}
      >
        {POPULAR_CITIES.map(city => {
          const selected = selectedCity === city;
          return (
            <Pressable
              key={city}
              onPress={() => {
                Haptics.selectionAsync().catch(() => {});
                setSelectedCity(city);
              }}
              style={[styles.cityChip, selected && styles.cityChipSelected]}
            >
              <Text style={[styles.cityChipText, selected && styles.cityChipTextSelected]}>
                {city}
              </Text>
            </Pressable>
          );
        })}
      </ScrollView>

      {/* Section Subtitle & Count */}
      <View style={styles.feedCountRow}>
        <Text style={styles.feedSectionLabel}>Keşfedilecek Kişiler</Text>
        <Text style={styles.feedCountText}>
          {filteredProfiles.length} kişi aktif
        </Text>
      </View>

      {/* Profiles Feed: Clean WhatsApp / Telegram List Item without text box */}
      {isLoading ? (
        <Card style={styles.loadingCard}>
          <ActivityIndicator color={colors.violet} size="large" />
          <Text style={styles.loadingText}>Açık profiller yükleniyor…</Text>
        </Card>
      ) : filteredProfiles.length > 0 ? (
        <View style={styles.profilesFeed}>
          {filteredProfiles.map(person => {
            const hasExistingChat = convMap.has(person.userId);
            const userStories = storiesByUser.get(person.userId);
            const hasStories = Boolean(userStories && userStories.length > 0);
            const photoAssetId = primaryPhotoMap.get(person.userId);

            return (
              <Card key={person.userId} style={styles.profileRowCard}>
                <View style={styles.cardContentRow}>
                  {/* Left Side: Avatar with burning/flaming story ring if stories exist */}
                  <Pressable
                    onPress={() => handleAvatarPress(person)}
                    style={styles.avatarContainer}
                  >
                    {hasStories ? (
                      <LinearGradient
                        colors={['#FF007A', '#FF4500', '#FFD600', '#FF0055']}
                        start={{ x: 0, y: 0 }}
                        end={{ x: 1, y: 1 }}
                        style={styles.flamingStoryRing}
                      >
                        <View style={styles.flamingInner}>
                          <Avatar
                            name={person.displayName}
                            mediaAssetId={photoAssetId}
                            size={56}
                          />
                        </View>
                        <View style={styles.flameBadge}>
                          <Sparkles size={10} color="#FFF" />
                        </View>
                      </LinearGradient>
                    ) : (
                      <View style={styles.normalAvatarRing}>
                        <Avatar
                          name={person.displayName}
                          mediaAssetId={photoAssetId}
                          size={56}
                        />
                      </View>
                    )}
                    <View style={styles.onlineDot} />
                  </Pressable>

                  {/* Right Side: Tapping the card body opens the chat screen directly! */}
                  <Pressable
                    onPress={() => handleCardBodyPress(person)}
                    style={styles.cardBodyRight}
                  >
                    <View style={styles.topInfoRow}>
                      <View style={styles.nameRow}>
                        <Text style={styles.nameText} numberOfLines={1}>
                          {person.displayName}
                        </Text>
                        {person.isVerified && (
                          <UserCheck size={14} color={colors.violet} />
                        )}
                        {(person.age || person.city) && (
                          <View style={styles.ageCityBadge}>
                            <Text style={styles.ageCityText}>
                              {person.age ? `${person.age}` : ''}
                              {person.age && person.city ? ' · ' : ''}
                              {person.city || ''}
                            </Text>
                          </View>
                        )}
                      </View>

                      {hasExistingChat && (
                        <View style={styles.activeChatBadge}>
                          <Text style={styles.activeChatText}>Sohbette</Text>
                        </View>
                      )}
                    </View>

                    <Text style={styles.nicknameText}>@{person.nickname || 'mindmatch'}</Text>

                    <Text style={styles.statusQuote} numberOfLines={2}>
                      {person.bio || 'Merhaba! MindMatch’te tanışmak ve sohbet etmek için mesaj atabilirsin.'}
                    </Text>

                    {person.interestIds?.length > 0 && (
                      <View style={styles.tagWrap}>
                        {person.interestIds.slice(0, 3).map(id => (
                          <View key={id} style={styles.tagPill}>
                            <Text style={styles.tagPillText}>#{interestNames.get(id) || 'İlgi'}</Text>
                          </View>
                        ))}
                      </View>
                    )}

                    {/* Action Prompt */}
                    <View style={styles.chatActionHint}>
                      <MessageCircle size={14} color={colors.violet} />
                      <Text style={styles.chatActionHintText}>
                        {hasExistingChat ? 'Sohbete Devam Et' : 'Sohbet Başlat'}
                      </Text>
                      <ChevronRight size={14} color={colors.muted} />
                    </View>
                  </Pressable>
                </View>
              </Card>
            );
          })}
        </View>
      ) : (
        <Card style={styles.emptyWrap}>
          <Empty
            icon="✦"
            title="Aradığın kriterlerde kimse bulunamadı"
            detail="Farklı bir arama kelimesi veya şehir deneyebilirsin."
            action={
              <Button
                title="Tüm Profilleri Gör"
                onPress={() => {
                  setSearchQuery('');
                  setSelectedCity('Tümü');
                }}
                style={{ marginTop: 14 }}
              />
            }
          />
        </Card>
      )}

      {/* Start New Chat Modal (Opened when tapping a user who has no existing conversation) */}
      <Modal
        visible={Boolean(activeNewChatTarget)}
        transparent
        animationType="fade"
        onRequestClose={() => setActiveNewChatTarget(null)}
      >
        <View style={styles.modalBackdrop}>
          <Pressable style={StyleSheet.absoluteFill} onPress={() => setActiveNewChatTarget(null)} />
          <View style={styles.chatModalContent}>
            <View style={styles.modalGrabber} />

            <View style={styles.chatModalHeader}>
              <Avatar
                name={activeNewChatTarget?.displayName || 'Kullanıcı'}
                mediaAssetId={activeNewChatTarget ? primaryPhotoMap.get(activeNewChatTarget.userId) : undefined}
                size={52}
              />
              <View style={{ flex: 1, marginLeft: 12 }}>
                <Text style={styles.chatModalTitle}>{activeNewChatTarget?.displayName}</Text>
                <Text style={styles.chatModalSub}>İlk mesajını gönder ve sohbeti başlat</Text>
              </View>
              <Pressable onPress={() => setActiveNewChatTarget(null)}>
                <X size={20} color={colors.muted} />
              </Pressable>
            </View>

            <TextInput
              value={firstMessageText}
              onChangeText={setFirstMessageText}
              placeholder="İlk mesajını yaz…"
              placeholderTextColor={colors.muted}
              multiline
              maxLength={400}
              style={styles.chatModalInput}
            />

            <View style={styles.chatModalButtons}>
              <Button
                title="Profili Gör"
                variant="outline"
                onPress={() => {
                  const uid = activeNewChatTarget?.userId;
                  setActiveNewChatTarget(null);
                  if (uid) router.push({ pathname: '/user/[id]', params: { id: uid } });
                }}
                style={{ flex: 1 }}
              />
              <Button
                title="Sohbeti Başlat"
                icon={<Send size={16} color={colors.white} />}
                loading={startConversation.isPending}
                disabled={!firstMessageText.trim()}
                onPress={() => {
                  if (activeNewChatTarget) {
                    startConversation.mutate({
                      recipientId: activeNewChatTarget.userId,
                      text: firstMessageText.trim(),
                    });
                  }
                }}
                style={{ flex: 1 }}
              />
            </View>
          </View>
        </View>
      </Modal>

      {/* Filter Bottom Sheet Modal */}
      <Modal
        visible={filterModalVisible}
        transparent
        animationType="slide"
        onRequestClose={() => setFilterModalVisible(false)}
      >
        <View style={styles.modalBackdrop}>
          <Pressable style={StyleSheet.absoluteFill} onPress={() => setFilterModalVisible(false)} />
          <View style={styles.modalContent}>
            <View style={styles.modalGrabber} />

            <View style={styles.modalHeader}>
              <View style={{ flexDirection: 'row', alignItems: 'center', gap: 8 }}>
                <SlidersHorizontal size={20} color={colors.violet} />
                <Text style={styles.modalHeaderTitle}>Keşif Filtreleri</Text>
              </View>
              <Pressable onPress={() => setFilterModalVisible(false)}>
                <X size={22} color={colors.muted} />
              </Pressable>
            </View>

            {/* Free Filters: City Selection */}
            <Text style={styles.modalSectionLabel}>ŞEHİR FİLTRESİ (Ücretsiz)</Text>
            <ScrollView horizontal showsHorizontalScrollIndicator={false} style={{ marginBottom: 20 }}>
              <View style={{ flexDirection: 'row', gap: 8 }}>
                {POPULAR_CITIES.map(city => (
                  <Pressable
                    key={city}
                    onPress={() => setSelectedCity(city)}
                    style={[
                      styles.cityChip,
                      selectedCity === city && styles.cityChipSelected,
                    ]}
                  >
                    <Text style={[styles.cityChipText, selectedCity === city && styles.cityChipTextSelected]}>
                      {city}
                    </Text>
                  </Pressable>
                ))}
              </View>
            </ScrollView>

            {/* Premium Gated Filters: Age & Gender */}
            <View style={styles.premiumBox}>
              <View style={styles.premiumBoxHeader}>
                <View style={{ flexDirection: 'row', alignItems: 'center', gap: 6 }}>
                  <Crown size={18} color="#E5A100" />
                  <Text style={styles.premiumBoxTitle}>Yaş & Tercih Filtreleri</Text>
                </View>
                {!isPremium && (
                  <View style={styles.premiumLockBadge}>
                    <Lock size={12} color="#E5A100" />
                    <Text style={styles.premiumLockText}>PREMIUM</Text>
                  </View>
                )}
              </View>

              {!isPremium ? (
                <View style={styles.premiumLockNotice}>
                  <Text style={styles.premiumLockDesc}>
                    Yaş aralığı ve cinsiyet gibi detaylı arama parametreleri Premium üyelere özeldir.
                  </Text>
                  <Button
                    title="Premium'a Yükselt 👑"
                    onPress={() => {
                      setFilterModalVisible(false);
                      router.push('/premium');
                    }}
                    style={{ marginTop: 12 }}
                  />
                </View>
              ) : (
                <View style={styles.premiumActiveControls}>
                  <Text style={styles.inputLabel}>Minimum Yaş: {minAge}</Text>
                  <TextInput
                    value={minAge}
                    onChangeText={setMinAge}
                    keyboardType="number-pad"
                    style={styles.numberInput}
                    placeholder="18"
                  />

                  <Text style={[styles.inputLabel, { marginTop: 12 }]}>Maksimum Yaş: {maxAge}</Text>
                  <TextInput
                    value={maxAge}
                    onChangeText={setMaxAge}
                    keyboardType="number-pad"
                    style={styles.numberInput}
                    placeholder="40"
                  />

                  <View style={{ marginTop: 14 }}>
                    <Text style={styles.inputLabel}>Cinsiyet Tercihi</Text>
                    <View style={styles.genderRow}>
                      {(['all', 'female', 'male'] as const).map(g => (
                        <Pressable
                          key={g}
                          onPress={() => setGenderFilter(g)}
                          style={[
                            styles.genderChip,
                            genderFilter === g && styles.genderChipSelected,
                          ]}
                        >
                          <Text
                            style={[
                              styles.genderChipText,
                              genderFilter === g && styles.genderChipTextSelected,
                            ]}
                          >
                            {g === 'all' ? 'Tümü' : g === 'female' ? 'Kadın' : 'Erkek'}
                          </Text>
                        </Pressable>
                      ))}
                    </View>
                  </View>
                </View>
              )}
            </View>

            <Button
              title="Filtreleri Uygula"
              onPress={() => setFilterModalVisible(false)}
              style={{ marginTop: 18 }}
            />
          </View>
        </View>
      </Modal>
    </Screen>
  );
}

const styles = StyleSheet.create({
  screen: { paddingHorizontal: 16, paddingTop: 10, paddingBottom: 40 },
  topHeader: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    marginBottom: 16,
  },
  eyebrow: {
    fontSize: 10,
    letterSpacing: 1.5,
    fontWeight: '900',
    color: colors.violet,
  },
  title: {
    fontSize: 28,
    fontWeight: '900',
    letterSpacing: -0.8,
    color: colors.ink,
    marginTop: 2,
  },
  topHeaderActions: { flexDirection: 'row', gap: 10 },
  iconCircle: {
    width: 42,
    height: 42,
    borderRadius: 14,
    backgroundColor: colors.white,
    borderWidth: 1,
    borderColor: colors.line,
    alignItems: 'center',
    justifyContent: 'center',
  },
  searchRow: { flexDirection: 'row', gap: 10, alignItems: 'center', marginBottom: 12 },
  searchInputWrap: {
    flex: 1,
    height: 48,
    backgroundColor: colors.white,
    borderRadius: 16,
    borderWidth: 1,
    borderColor: colors.line,
    flexDirection: 'row',
    alignItems: 'center',
  },
  searchInput: {
    flex: 1,
    fontSize: 14,
    color: colors.ink,
    paddingHorizontal: 10,
  },
  filterBtn: {
    width: 48,
    height: 48,
    borderRadius: 16,
    backgroundColor: colors.white,
    borderWidth: 1,
    borderColor: colors.line,
    alignItems: 'center',
    justifyContent: 'center',
  },
  filterBtnPremium: {
    backgroundColor: colors.violet,
    borderColor: colors.violet,
  },
  cityChipsRow: { gap: 8, paddingBottom: 10 },
  cityChip: {
    paddingHorizontal: 14,
    paddingVertical: 8,
    borderRadius: 14,
    backgroundColor: colors.white,
    borderWidth: 1,
    borderColor: colors.line,
  },
  cityChipSelected: {
    backgroundColor: colors.violet,
    borderColor: colors.violet,
  },
  cityChipText: { fontSize: 12, fontWeight: '700', color: colors.muted },
  cityChipTextSelected: { color: colors.white },
  feedCountRow: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    marginTop: 6,
    marginBottom: 14,
  },
  feedSectionLabel: { fontSize: 16, fontWeight: '900', color: colors.ink },
  feedCountText: { fontSize: 12, fontWeight: '700', color: colors.muted },
  profilesFeed: { gap: 12 },
  profileRowCard: {
    padding: 14,
    borderRadius: 20,
    borderWidth: 1,
    borderColor: colors.line,
    backgroundColor: colors.white,
  },
  cardContentRow: { flexDirection: 'row', alignItems: 'flex-start' },
  avatarContainer: { position: 'relative', marginRight: 14 },
  flamingStoryRing: {
    width: 66,
    height: 66,
    borderRadius: 33,
    padding: 2.8,
    alignItems: 'center',
    justifyContent: 'center',
    shadowColor: '#FF4500',
    shadowOffset: { width: 0, height: 2 },
    shadowOpacity: 0.5,
    shadowRadius: 6,
    elevation: 6,
  },
  flamingInner: {
    width: 60,
    height: 60,
    borderRadius: 30,
    backgroundColor: colors.white,
    alignItems: 'center',
    justifyContent: 'center',
  },
  flameBadge: {
    position: 'absolute',
    top: -2,
    right: -2,
    backgroundColor: '#FF4500',
    width: 18,
    height: 18,
    borderRadius: 9,
    alignItems: 'center',
    justifyContent: 'center',
    borderWidth: 1.5,
    borderColor: colors.white,
  },
  normalAvatarRing: {
    width: 62,
    height: 62,
    borderRadius: 31,
    borderWidth: 1.5,
    borderColor: colors.line,
    alignItems: 'center',
    justifyContent: 'center',
  },
  onlineDot: {
    position: 'absolute',
    bottom: 2,
    right: 2,
    width: 13,
    height: 13,
    borderRadius: 6.5,
    backgroundColor: '#10B981',
    borderWidth: 2,
    borderColor: colors.white,
  },
  cardBodyRight: { flex: 1 },
  topInfoRow: { flexDirection: 'row', justifyContent: 'space-between', alignItems: 'center' },
  nameRow: { flexDirection: 'row', alignItems: 'center', gap: 6, flexWrap: 'wrap', flex: 1 },
  nameText: { fontSize: 16, fontWeight: '900', color: colors.ink },
  ageCityBadge: {
    backgroundColor: colors.paleViolet,
    paddingHorizontal: 8,
    paddingVertical: 2,
    borderRadius: 8,
  },
  ageCityText: { fontSize: 11, fontWeight: '800', color: colors.violet },
  nicknameText: { fontSize: 12, fontWeight: '600', color: colors.muted, marginTop: 2 },
  activeChatBadge: {
    backgroundColor: '#E8F5E9',
    paddingHorizontal: 8,
    paddingVertical: 3,
    borderRadius: 8,
  },
  activeChatText: { fontSize: 10, fontWeight: '900', color: '#2E7D32' },
  statusQuote: {
    fontSize: 13,
    color: colors.ink,
    lineHeight: 18,
    marginTop: 6,
  },
  tagWrap: { flexDirection: 'row', flexWrap: 'wrap', gap: 6, marginTop: 8 },
  tagPill: {
    backgroundColor: colors.canvas,
    borderWidth: 1,
    borderColor: colors.line,
    paddingHorizontal: 8,
    paddingVertical: 3,
    borderRadius: 10,
  },
  tagPillText: { fontSize: 10, fontWeight: '700', color: colors.muted },
  chatActionHint: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 5,
    marginTop: 10,
    alignSelf: 'flex-start',
  },
  chatActionHintText: {
    fontSize: 12,
    fontWeight: '800',
    color: colors.violet,
  },
  loadingCard: { padding: 32, alignItems: 'center', gap: 12, borderRadius: 20 },
  loadingText: { fontSize: 13, color: colors.muted, fontWeight: '700' },
  emptyWrap: { padding: 24, borderRadius: 20 },
  modalBackdrop: { flex: 1, backgroundColor: 'rgba(0,0,0,0.5)', justifyContent: 'flex-end' },
  chatModalContent: {
    backgroundColor: colors.white,
    borderTopLeftRadius: 26,
    borderTopRightRadius: 26,
    padding: 20,
    paddingBottom: 36,
  },
  chatModalHeader: { flexDirection: 'row', alignItems: 'center', marginBottom: 16 },
  chatModalTitle: { fontSize: 17, fontWeight: '900', color: colors.ink },
  chatModalSub: { fontSize: 12, color: colors.muted, marginTop: 2 },
  chatModalInput: {
    minHeight: 80,
    borderRadius: 14,
    borderWidth: 1,
    borderColor: colors.line,
    backgroundColor: colors.canvas,
    padding: 12,
    fontSize: 14,
    color: colors.ink,
    textAlignVertical: 'top',
    marginBottom: 16,
  },
  chatModalButtons: { flexDirection: 'row', gap: 10 },
  modalContent: {
    backgroundColor: colors.white,
    borderTopLeftRadius: 26,
    borderTopRightRadius: 26,
    padding: 20,
    paddingBottom: 36,
  },
  modalGrabber: {
    width: 40,
    height: 4,
    borderRadius: 2,
    backgroundColor: '#DDD',
    alignSelf: 'center',
    marginBottom: 16,
  },
  modalHeader: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    marginBottom: 16,
  },
  modalHeaderTitle: { fontSize: 18, fontWeight: '900', color: colors.ink },
  modalSectionLabel: {
    fontSize: 10,
    letterSpacing: 1.2,
    fontWeight: '900',
    color: colors.muted,
    marginBottom: 10,
  },
  premiumBox: {
    borderWidth: 1,
    borderColor: '#F3E5AB',
    backgroundColor: '#FFFDF5',
    borderRadius: 18,
    padding: 16,
    marginTop: 8,
  },
  premiumBoxHeader: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    marginBottom: 10,
  },
  premiumBoxTitle: { fontSize: 14, fontWeight: '900', color: colors.ink },
  premiumLockBadge: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 4,
    backgroundColor: '#FFF4D6',
    paddingHorizontal: 8,
    paddingVertical: 3,
    borderRadius: 8,
  },
  premiumLockText: { fontSize: 10, fontWeight: '900', color: '#B57C00' },
  premiumLockNotice: { alignItems: 'center', paddingVertical: 8 },
  premiumLockDesc: {
    fontSize: 12,
    color: colors.muted,
    textAlign: 'center',
    lineHeight: 17,
  },
  premiumActiveControls: { marginTop: 6 },
  inputLabel: { fontSize: 12, fontWeight: '800', color: colors.ink, marginBottom: 4 },
  numberInput: {
    height: 40,
    borderWidth: 1,
    borderColor: colors.line,
    borderRadius: 10,
    paddingHorizontal: 12,
    fontSize: 14,
    backgroundColor: colors.white,
  },
  genderRow: { flexDirection: 'row', gap: 8, marginTop: 6 },
  genderChip: {
    flex: 1,
    paddingVertical: 8,
    alignItems: 'center',
    borderRadius: 10,
    backgroundColor: colors.white,
    borderWidth: 1,
    borderColor: colors.line,
  },
  genderChipSelected: {
    backgroundColor: colors.violet,
    borderColor: colors.violet,
  },
  genderChipText: { fontSize: 12, fontWeight: '700', color: colors.muted },
  genderChipTextSelected: { color: colors.white },
});
