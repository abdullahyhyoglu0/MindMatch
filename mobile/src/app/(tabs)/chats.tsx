import { useQueries, useQuery } from '@tanstack/react-query';
import { useMemo, useState } from 'react';
import { router } from 'expo-router';
import { ChevronRight, MessageCircle, MessageSquarePlus, Search, Sparkles, X } from 'lucide-react-native';
import { FlatList, Pressable, StyleSheet, Text, TextInput, View } from 'react-native';
import { chatApi, presenceApi } from '../../api/mindmatch';
import { Avatar, Card, Empty, Loading, Screen, Title } from '../../components/ui';
import { colors } from '../../theme/tokens';

export default function ChatsScreen() {
  const [search, setSearch] = useState('');
  const list = useQuery({ queryKey: ['conversations'], queryFn: chatApi.conversations, refetchInterval: 30000 });
  const people = useQueries({
    queries: (list.data || []).map(c => ({ queryKey: ['profile', c.otherUserId], queryFn: () => chatApi.profile(c.otherUserId) })),
  });
  const presence = useQueries({
    queries: (list.data || []).map(c => ({ queryKey: ['presence', c.otherUserId], queryFn: () => presenceApi.get(c.otherUserId), refetchInterval: 30000 })),
  });

  const filtered = useMemo(() => {
    const needle = search.trim().toLocaleLowerCase('tr-TR');
    if (!needle) return list.data ?? [];
    return (list.data ?? []).filter((conversation, index) => {
      const person = people[index]?.data;
      return [person?.displayName, person?.nickname].some(value => value?.toLocaleLowerCase('tr-TR').includes(needle));
    });
  }, [search, list.data, people]);

  if (list.isLoading) return <Screen><Loading label="Sohbetler yükleniyor…" /></Screen>;

  return (
    <Screen style={styles.screen}>
      {/* Header */}
      <View style={styles.topHeader}>
        <View>
          <Text style={styles.eyebrow}>BAĞLANTILARIN</Text>
          <Text style={styles.pageTitle}>Sohbetler</Text>
        </View>
        <Pressable
          accessibilityRole="button"
          accessibilityLabel="Mesaj istekleri"
          style={styles.requestButton}
          onPress={() => router.push('/requests')}
        >
          <MessageSquarePlus size={20} color={colors.violet} strokeWidth={2.2} />
        </Pressable>
      </View>

      {/* Modern Search Bar */}
      <View style={styles.searchBar}>
        <Search size={18} color="#A29EB5" />
        <TextInput
          value={search}
          onChangeText={setSearch}
          placeholder="İsim veya kullanıcı adı ara…"
          placeholderTextColor="#A29EB5"
          style={styles.searchInput}
        />
        {search ? (
          <Pressable onPress={() => setSearch('')} style={styles.searchClearBtn}>
            <X size={14} color={colors.muted} />
          </Pressable>
        ) : null}
      </View>

      {/* Conversation List */}
      <FlatList
        data={filtered}
        keyExtractor={x => x.id}
        contentContainerStyle={styles.listContent}
        ItemSeparatorComponent={() => <View style={styles.separator} />}
        renderItem={({ item }) => {
          const index = (list.data ?? []).findIndex(value => value.id === item.id);
          const p = people[index]?.data;
          const status = presence[index]?.data;
          const isOnline = Boolean(status?.isOnline);
          const lastSeen = isOnline
            ? 'Çevrimiçi'
            : status?.lastSeenAtUtc
            ? `Son görülme ${new Date(status.lastSeenAtUtc).toLocaleString('tr-TR', { day: 'numeric', month: 'short', hour: '2-digit', minute: '2-digit' })}`
            : 'Sohbet aktif';

          return (
            <Pressable
              onPress={() => router.push({
                pathname: '/conversation/[id]',
                params: { id: item.id, userId: item.otherUserId, name: p?.displayName || 'Bağlantı' },
              })}
              style={({ pressed }) => [styles.chatRow, pressed && styles.chatRowPressed]}
            >
              {/* Avatar with Online Glow */}
              <View style={styles.avatarContainer}>
                <Pressable
                  accessibilityRole="button"
                  accessibilityLabel="Profili görüntüle"
                  onPress={() => router.push({ pathname: '/user/[id]', params: { id: item.otherUserId } })}
                >
                  <Avatar name={p?.displayName || 'MM'} size={54} />
                </Pressable>
                {isOnline ? <View style={styles.onlineDot} /> : null}
              </View>

              {/* Chat Info */}
              <View style={styles.chatInfo}>
                <View style={styles.chatNameRow}>
                  <Text numberOfLines={1} style={styles.chatName}>
                    {p?.displayName || 'MindMatch kullanıcısı'}
                  </Text>
                  <Text style={styles.chatTime}>
                    {item.lastMessageAtUtc
                      ? new Date(item.lastMessageAtUtc).toLocaleDateString('tr-TR', { day: 'numeric', month: 'short' })
                      : ''}
                  </Text>
                </View>

                <View style={styles.chatSubRow}>
                  <Text numberOfLines={1} style={[styles.chatStatusText, isOnline && styles.chatStatusOnline]}>
                    {isOnline ? '● ' : ''}{lastSeen}
                  </Text>
                </View>
              </View>

              {/* Chevron */}
              <ChevronRight size={17} color="#BFBACC" strokeWidth={2.4} />
            </Pressable>
          );
        }}
        ListEmptyComponent={
          <Card style={styles.emptyCard}>
            <Empty
              icon={search ? '⌕' : '♡'}
              title={search ? 'Eşleşen sohbet yok' : 'Henüz sohbetin yok'}
              detail={search ? 'Aramayı kontrol edip tekrar deneyebilirsin.' : 'Keşfet bölümünden yeni insanlarla tanışarak ilk sohbetini başlatabilirsin.'}
              action={
                !search ? (
                  <Pressable onPress={() => router.replace('/(tabs)')} style={styles.discoverCTA}>
                    <Text style={styles.discoverCTAText}>Kişileri Keşfet →</Text>
                  </Pressable>
                ) : undefined
              }
            />
          </Card>
        }
      />
    </Screen>
  );
}

const styles = StyleSheet.create({
  screen: {
    paddingHorizontal: 18,
    paddingTop: 12,
    backgroundColor: '#F8F7FC',
  },
  topHeader: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'space-between',
    marginBottom: 16,
  },
  eyebrow: {
    color: colors.violet,
    fontSize: 9,
    fontWeight: '900',
    letterSpacing: 1.4,
  },
  pageTitle: {
    color: colors.ink,
    fontSize: 28,
    fontWeight: '900',
    letterSpacing: -0.8,
    marginTop: 2,
  },
  requestButton: {
    width: 42,
    height: 42,
    borderRadius: 14,
    borderWidth: 1,
    borderColor: '#ECE8F5',
    backgroundColor: colors.white,
    alignItems: 'center',
    justifyContent: 'center',
    shadowColor: '#171329',
    shadowOffset: { width: 0, height: 2 },
    shadowOpacity: 0.04,
    shadowRadius: 6,
    elevation: 2,
  },

  // Search Bar
  searchBar: {
    height: 46,
    borderRadius: 16,
    backgroundColor: colors.white,
    borderWidth: 1,
    borderColor: '#ECE8F5',
    flexDirection: 'row',
    alignItems: 'center',
    paddingHorizontal: 14,
    gap: 10,
    marginBottom: 16,
    shadowColor: '#171329',
    shadowOffset: { width: 0, height: 2 },
    shadowOpacity: 0.03,
    shadowRadius: 6,
    elevation: 1,
  },
  searchInput: {
    flex: 1,
    fontSize: 13,
    color: colors.ink,
  },
  searchClearBtn: {
    padding: 4,
  },

  // List & Row
  listContent: {
    paddingBottom: 40,
    flexGrow: 1,
  },
  chatRow: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 13,
    paddingVertical: 12,
    paddingHorizontal: 6,
  },
  chatRowPressed: {
    opacity: 0.75,
    backgroundColor: '#F2EEF9',
    borderRadius: 16,
  },
  avatarContainer: {
    position: 'relative',
  },
  onlineDot: {
    position: 'absolute',
    bottom: 0,
    right: 0,
    width: 13,
    height: 13,
    borderRadius: 7,
    backgroundColor: colors.green,
    borderWidth: 2,
    borderColor: colors.white,
  },
  chatInfo: {
    flex: 1,
    minWidth: 0,
  },
  chatNameRow: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
  },
  chatName: {
    fontSize: 14,
    fontWeight: '800',
    color: colors.ink,
    letterSpacing: -0.2,
  },
  chatTime: {
    fontSize: 10,
    color: '#A29EB5',
    fontWeight: '600',
  },
  chatSubRow: {
    marginTop: 4,
  },
  chatStatusText: {
    fontSize: 11,
    color: colors.muted,
  },
  chatStatusOnline: {
    color: colors.green,
    fontWeight: '800',
  },
  separator: {
    height: 1,
    backgroundColor: '#EDE9F4',
    marginLeft: 67,
  },

  // Empty
  emptyCard: {
    marginTop: 24,
    padding: 24,
    borderRadius: 22,
    alignItems: 'center',
  },
  discoverCTA: {
    marginTop: 16,
    paddingVertical: 10,
    paddingHorizontal: 18,
    borderRadius: 14,
    backgroundColor: colors.paleViolet,
  },
  discoverCTAText: {
    color: colors.violet,
    fontSize: 12,
    fontWeight: '800',
  },
});
