import { useMemo } from 'react';
import { Alert, Pressable, StyleSheet, Text, View } from 'react-native';
import { router } from 'expo-router';
import { useMutation, useQueries, useQuery, useQueryClient } from '@tanstack/react-query';
import { ArrowLeft, Check, Heart, MessageCircle, Sparkles, UserCheck, X } from 'lucide-react-native';
import { chatApi, matchApi, notificationApi } from '../api/mindmatch';
import { Avatar, Button, Card, Empty, Loading, Screen } from '../components/ui';
import { colors } from '../theme/tokens';

export default function NotificationsPage() {
  const qc = useQueryClient();
  const notifications = useQuery({ queryKey: ['notifications'], queryFn: notificationApi.list, refetchInterval: 10_000 });
  const matches = useQuery({ queryKey: ['matches', 'requests'], queryFn: matchApi.requests, refetchInterval: 10_000 });
  const messageRequests = useQuery({ queryKey: ['message-requests'], queryFn: chatApi.incoming, refetchInterval: 10_000 });

  const personIds = useMemo(() => [
    ...new Set([
      ...(matches.data ?? []).map(request => request.requesterId),
      ...(messageRequests.data ?? []).map(request => request.senderId),
    ]),
  ], [matches.data, messageRequests.data]);

  const people = useQueries({
    queries: personIds.map(userId => ({ queryKey: ['profile', userId], queryFn: () => chatApi.profile(userId) })),
  });
  const profileById = new Map(personIds.map((id, index) => [id, people[index]?.data]));

  const refreshRequests = async () => {
    await Promise.all([
      qc.invalidateQueries({ queryKey: ['matches'] }),
      qc.invalidateQueries({ queryKey: ['message-requests'] }),
      qc.invalidateQueries({ queryKey: ['conversations'] }),
      qc.invalidateQueries({ queryKey: ['notifications'] }),
    ]);
  };

  const acceptMatch = useMutation({
    mutationFn: matchApi.accept,
    onSuccess: async match => {
      await refreshRequests();
      const conversations = await qc.fetchQuery({ queryKey: ['conversations'], queryFn: chatApi.conversations });
      const conversation = conversations.find(item => item.otherUserId === match.requesterId);
      if (conversation) router.push({ pathname: '/conversation/[id]', params: { id: conversation.id, userId: match.requesterId } });
    },
    onError: error => Alert.alert('İstek kabul edilemedi', error.message),
  });

  const rejectMatch = useMutation({
    mutationFn: matchApi.reject,
    onSuccess: refreshRequests,
    onError: error => Alert.alert('İstek reddedilemedi', error.message),
  });

  const acceptMessage = useMutation({
    mutationFn: chatApi.acceptMessageRequest,
    onSuccess: (_result, conversationId) => {
      const request = messageRequests.data?.find(item => item.conversationId === conversationId);
      refreshRequests();
      router.push({ pathname: '/conversation/[id]', params: { id: conversationId, userId: request?.senderId ?? '' } });
    },
    onError: error => Alert.alert('Mesaj isteği kabul edilemedi', error.message),
  });

  const rejectMessage = useMutation({
    mutationFn: chatApi.rejectMessageRequest,
    onSuccess: refreshRequests,
    onError: error => Alert.alert('Mesaj isteği reddedilemedi', error.message),
  });

  const markAll = useMutation({
    mutationFn: notificationApi.readAll,
    onSuccess: () => qc.invalidateQueries({ queryKey: ['notifications'] }),
  });

  const markOne = useMutation({
    mutationFn: notificationApi.read,
    onSuccess: () => qc.invalidateQueries({ queryKey: ['notifications'] }),
  });

  if (notifications.isLoading || matches.isLoading || messageRequests.isLoading) {
    return <Screen><Loading label="Bildirimler yükleniyor" /></Screen>;
  }

  const items = notifications.data?.items ?? [];
  const pendingRequestsCount = (matches.data?.length ?? 0) + (messageRequests.data?.length ?? 0);

  return (
    <Screen scroll style={styles.screen}>
      <View style={styles.topHeader}>
        <Pressable onPress={() => router.back()} style={styles.backBtn}>
          <ArrowLeft size={20} color={colors.ink} />
        </Pressable>
        <View style={{ flex: 1, marginLeft: 12 }}>
          <Text style={styles.topKicker}>AKTİVİTELER & İSTEKLER</Text>
          <Text style={styles.topTitle}>Bildirimler</Text>
        </View>
        {items.length > 0 && (
          <Pressable
            accessibilityRole="button"
            onPress={() => markAll.mutate()}
            disabled={markAll.isPending}
            style={styles.markAllBtn}
          >
            <Text style={styles.action}>Tümünü Oku</Text>
          </Pressable>
        )}
      </View>

      {pendingRequestsCount > 0 ? (
        <View style={styles.sectionWrap}>
          <View style={styles.sectionHeaderRow}>
            <Text style={styles.sectionTitle}>Yanıt Bekleyen İstekler</Text>
            <View style={styles.badge}>
              <Text style={styles.badgeText}>{pendingRequestsCount}</Text>
            </View>
          </View>

          {matches.data?.map(request => {
            const person = profileById.get(request.requesterId);
            return (
              <Card key={request.id} style={styles.requestCard}>
                <Pressable
                  accessibilityRole="button"
                  onPress={() => router.push({ pathname: '/user/[id]', params: { id: request.requesterId } })}
                  style={styles.person}
                >
                  <Avatar name={person?.displayName ?? 'MindMatch'} size={50} />
                  <View style={styles.personCopy}>
                    <Text style={styles.personName}>{person?.displayName ?? 'Yeni Bağlantı'}</Text>
                    <Text style={styles.personDetail}>Seninle eşleşmek istiyor</Text>
                  </View>
                  <View style={styles.requestIconBadge}>
                    <Heart size={16} color={colors.coral} fill={colors.coral} />
                  </View>
                </Pressable>
                <View style={styles.buttons}>
                  <Button
                    title="Reddet"
                    variant="outline"
                    onPress={() => rejectMatch.mutate(request.id)}
                    loading={rejectMatch.isPending && rejectMatch.variables === request.id}
                    style={styles.flex}
                  />
                  <Button
                    title="Kabul Et"
                    onPress={() => acceptMatch.mutate(request.id)}
                    loading={acceptMatch.isPending && acceptMatch.variables === request.id}
                    style={styles.flex}
                  />
                </View>
              </Card>
            );
          })}

          {messageRequests.data?.map(request => {
            const person = profileById.get(request.senderId);
            return (
              <Card key={request.conversationId} style={styles.requestCard}>
                <Pressable
                  accessibilityRole="button"
                  onPress={() => router.push({ pathname: '/user/[id]', params: { id: request.senderId } })}
                  style={styles.person}
                >
                  <Avatar name={person?.displayName ?? 'MindMatch'} size={50} />
                  <View style={styles.personCopy}>
                    <Text style={styles.personName}>{person?.displayName ?? 'Yeni Bağlantı'}</Text>
                    <Text numberOfLines={2} style={styles.personDetail}>{request.initialMessage || 'Sana mesaj isteği gönderdi.'}</Text>
                  </View>
                  <View style={styles.requestIconBadgeMessage}>
                    <MessageCircle size={16} color={colors.violet} />
                  </View>
                </Pressable>
                <View style={styles.buttons}>
                  <Button
                    title="Reddet"
                    variant="outline"
                    onPress={() => rejectMessage.mutate(request.conversationId)}
                    loading={rejectMessage.isPending && rejectMessage.variables === request.conversationId}
                    style={styles.flex}
                  />
                  <Button
                    title="Kabul Et"
                    onPress={() => acceptMessage.mutate(request.conversationId)}
                    loading={acceptMessage.isPending && acceptMessage.variables === request.conversationId}
                    style={styles.flex}
                  />
                </View>
              </Card>
            );
          })}
        </View>
      ) : null}

      <View style={styles.sectionWrap}>
        <Text style={styles.sectionTitle}>Son Bildirimler</Text>
        {items.length ? (
          items.map(item => (
            <Pressable
              key={item.id}
              accessibilityRole="button"
              onPress={() => { if (!item.readAtUtc) markOne.mutate(item.id); }}
            >
              <Card style={[styles.notice, !item.readAtUtc && styles.unread]}>
                <View style={[styles.icon, !item.readAtUtc && styles.unreadIcon]}>
                  {item.type === 1 ? (
                    <Heart color={colors.coral} size={18} fill={colors.coral} />
                  ) : item.type === 2 ? (
                    <UserCheck color={colors.green} size={18} />
                  ) : item.type === 3 || item.type === 4 || item.type === 5 ? (
                    <MessageCircle color={colors.violet} size={18} />
                  ) : (
                    <Sparkles color={colors.violet} size={18} />
                  )}
                </View>
                <View style={styles.noticeCopy}>
                  <Text style={styles.noticeTitle}>{item.title}</Text>
                  <Text style={styles.noticeBody}>{item.body}</Text>
                  <Text style={styles.date}>{new Date(item.createdAtUtc).toLocaleString('tr-TR', { dateStyle: 'short', timeStyle: 'short' })}</Text>
                </View>
                {!item.readAtUtc && <View style={styles.dot} />}
              </Card>
            </Pressable>
          ))
        ) : (
          <Card style={styles.emptyCard}>
            <Empty
              icon="✦"
              title={pendingRequestsCount > 0 ? 'İsteklerin hazır' : 'Henüz yeni bildirim yok'}
              detail={pendingRequestsCount > 0 ? 'Yukarıdaki istekleri onaylayarak sohbet başlatabilirsin.' : 'Yeni bir eşleşme, mesaj ya da hareket olduğunda burada görünecek.'}
            />
          </Card>
        )}
      </View>
    </Screen>
  );
}

const styles = StyleSheet.create({
  screen: { paddingHorizontal: 16, paddingTop: 10, paddingBottom: 40 },
  topHeader: { flexDirection: 'row', alignItems: 'center', marginBottom: 18 },
  backBtn: { width: 40, height: 40, borderRadius: 14, backgroundColor: colors.white, borderWidth: 1, borderColor: colors.line, alignItems: 'center', justifyContent: 'center' },
  topKicker: { fontSize: 9, letterSpacing: 1.5, fontWeight: '900', color: colors.violet, textTransform: 'uppercase' },
  topTitle: { fontSize: 24, fontWeight: '900', letterSpacing: -0.6, color: colors.ink, marginTop: 1 },
  markAllBtn: { paddingVertical: 6, paddingHorizontal: 10, borderRadius: 10, backgroundColor: colors.paleViolet },
  action: { fontSize: 11, color: colors.violet, fontWeight: '800' },
  sectionWrap: { marginBottom: 22 },
  sectionHeaderRow: { flexDirection: 'row', alignItems: 'center', gap: 8, marginBottom: 12 },
  sectionTitle: { fontSize: 15, fontWeight: '900', color: colors.ink, letterSpacing: -0.3 },
  badge: { backgroundColor: colors.coral, borderRadius: 10, paddingHorizontal: 7, paddingVertical: 2 },
  badgeText: { color: colors.white, fontSize: 10, fontWeight: '800' },
  requestCard: { padding: 16, marginBottom: 12, borderRadius: 18 },
  person: { flexDirection: 'row', alignItems: 'center', gap: 14 },
  personCopy: { flex: 1 },
  personName: { fontSize: 14, color: colors.ink, fontWeight: '900' },
  personDetail: { fontSize: 12, lineHeight: 17, color: colors.muted, marginTop: 3 },
  requestIconBadge: { width: 34, height: 34, borderRadius: 12, backgroundColor: '#FFF0F2', alignItems: 'center', justifyContent: 'center' },
  requestIconBadgeMessage: { width: 34, height: 34, borderRadius: 12, backgroundColor: colors.paleViolet, alignItems: 'center', justifyContent: 'center' },
  buttons: { flexDirection: 'row', gap: 10, marginTop: 14 },
  flex: { flex: 1 },
  notice: { padding: 14, marginBottom: 10, flexDirection: 'row', alignItems: 'flex-start', gap: 12, borderRadius: 16 },
  unread: { borderColor: '#DDD5FF', backgroundColor: '#FBF9FF' },
  icon: { width: 42, height: 42, borderRadius: 14, backgroundColor: colors.paleViolet, alignItems: 'center', justifyContent: 'center' },
  unreadIcon: { backgroundColor: '#EDE8FF' },
  noticeCopy: { flex: 1 },
  noticeTitle: { fontSize: 13, color: colors.ink, fontWeight: '800' },
  noticeBody: { fontSize: 12, lineHeight: 17, color: colors.muted, marginTop: 3 },
  date: { fontSize: 10, color: colors.faint, marginTop: 6, fontWeight: '600' },
  dot: { width: 8, height: 8, borderRadius: 4, backgroundColor: colors.coral, marginTop: 4 },
  emptyCard: { padding: 20, borderRadius: 18 },
});
