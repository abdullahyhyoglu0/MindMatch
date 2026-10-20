import { useMemo } from 'react';
import { Alert, Pressable, StyleSheet, Text, View } from 'react-native';
import { router } from 'expo-router';
import { useMutation, useQueries, useQuery, useQueryClient } from '@tanstack/react-query';
import { Check, Heart, MessageCircle, Sparkles } from 'lucide-react-native';
import { chatApi, matchApi, notificationApi } from '../../api/mindmatch';
import { Avatar, Button, Card, Empty, Loading, Screen, Title } from '../../components/ui';
import { colors } from '../../theme/tokens';

export default function NotificationsScreen() {
  const qc = useQueryClient();
  const notifications = useQuery({ queryKey: ['notifications'], queryFn: notificationApi.list, refetchInterval: 10_000 });
  const matches = useQuery({ queryKey: ['matches', 'requests'], queryFn: matchApi.requests, refetchInterval: 10_000 });
  const messageRequests = useQuery({ queryKey: ['message-requests'], queryFn: chatApi.incoming, refetchInterval: 10_000 });
  const personIds = useMemo(() => [...new Set([
    ...(matches.data ?? []).map(request => request.requesterId),
    ...(messageRequests.data ?? []).map(request => request.senderId),
  ])], [matches.data, messageRequests.data]);
  const people = useQueries({ queries: personIds.map(userId => ({ queryKey: ['profile', userId], queryFn: () => chatApi.profile(userId) })) });
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
  const rejectMatch = useMutation({ mutationFn: matchApi.reject, onSuccess: refreshRequests, onError: error => Alert.alert('İstek reddedilemedi', error.message) });
  const acceptMessage = useMutation({
    mutationFn: chatApi.acceptMessageRequest,
    onSuccess: (_result, conversationId) => {
      const request = messageRequests.data?.find(item => item.conversationId === conversationId);
      refreshRequests();
      router.push({ pathname: '/conversation/[id]', params: { id: conversationId, userId: request?.senderId ?? '' } });
    },
    onError: error => Alert.alert('Mesaj isteği kabul edilemedi', error.message),
  });
  const rejectMessage = useMutation({ mutationFn: chatApi.rejectMessageRequest, onSuccess: refreshRequests, onError: error => Alert.alert('Mesaj isteği reddedilemedi', error.message) });
  const markAll = useMutation({ mutationFn: notificationApi.readAll, onSuccess: () => qc.invalidateQueries({ queryKey: ['notifications'] }) });
  const markOne = useMutation({ mutationFn: notificationApi.read, onSuccess: () => qc.invalidateQueries({ queryKey: ['notifications'] }) });

  if (notifications.isLoading || matches.isLoading || messageRequests.isLoading) return <Screen><Loading label="Bildirimler yükleniyor" /></Screen>;
  const items = notifications.data?.items ?? [];

  return <Screen scroll style={styles.screen}>
    <Title eyebrow="GÜNCEL KAL" title="Bildirimler" subtitle="Yeni bağlantıları burada yanıtla." right={<Pressable accessibilityRole="button" onPress={() => markAll.mutate()} disabled={markAll.isPending}><Text style={styles.action}>Tümünü okundu yap</Text></Pressable>} />

    {(matches.data?.length ?? 0) + (messageRequests.data?.length ?? 0) > 0 ? <>
      <Text style={styles.section}>Yanıt bekleyen istekler</Text>
      {matches.data?.map(request => {
        const person = profileById.get(request.requesterId);
        return <Card key={request.id} style={styles.requestCard}>
          <Pressable accessibilityRole="button" onPress={() => router.push({ pathname: '/user/[id]', params: { id: request.requesterId } })} style={styles.person}>
            <Avatar name={person?.displayName ?? 'MindMatch'} size={48} />
            <View style={styles.personCopy}><Text style={styles.personName}>{person?.displayName ?? 'Yeni bağlantı'}</Text><Text style={styles.personDetail}>Seninle eşleşmek istiyor</Text></View>
            <Heart size={18} color={colors.coral} />
          </Pressable>
          <View style={styles.buttons}>
            <Button title="Reddet" variant="outline" onPress={() => rejectMatch.mutate(request.id)} loading={rejectMatch.isPending && rejectMatch.variables === request.id} style={styles.flex} />
            <Button title="Kabul et ve sohbete geç" onPress={() => acceptMatch.mutate(request.id)} loading={acceptMatch.isPending && acceptMatch.variables === request.id} style={styles.flex} />
          </View>
        </Card>;
      })}
      {messageRequests.data?.map(request => {
        const person = profileById.get(request.senderId);
        return <Card key={request.conversationId} style={styles.requestCard}>
          <Pressable accessibilityRole="button" onPress={() => router.push({ pathname: '/user/[id]', params: { id: request.senderId } })} style={styles.person}>
            <Avatar name={person?.displayName ?? 'MindMatch'} size={48} />
            <View style={styles.personCopy}><Text style={styles.personName}>{person?.displayName ?? 'Yeni bağlantı'}</Text><Text numberOfLines={2} style={styles.personDetail}>{request.initialMessage || 'Sana mesaj isteği gönderdi.'}</Text></View>
            <MessageCircle size={18} color={colors.violet} />
          </Pressable>
          <View style={styles.buttons}>
            <Button title="Reddet" variant="outline" onPress={() => rejectMessage.mutate(request.conversationId)} loading={rejectMessage.isPending && rejectMessage.variables === request.conversationId} style={styles.flex} />
            <Button title="Kabul et ve sohbete geç" onPress={() => acceptMessage.mutate(request.conversationId)} loading={acceptMessage.isPending && acceptMessage.variables === request.conversationId} style={styles.flex} />
          </View>
        </Card>;
      })}
    </> : null}

    <Text style={styles.section}>Son bildirimler</Text>
    {items.length ? items.map(item => <Pressable key={item.id} accessibilityRole="button" onPress={() => { if (!item.readAtUtc) markOne.mutate(item.id); }}>
      <Card style={[styles.notice, !item.readAtUtc && styles.unread]}>
        <View style={styles.icon}>{item.type === 1 ? <Heart color={colors.coral} size={18} /> : item.type === 2 ? <Check color={colors.green} size={18} /> : item.type === 3 || item.type === 4 || item.type === 5 ? <MessageCircle color={colors.violet} size={18} /> : <Sparkles color={colors.violet} size={18} />}</View>
        <View style={styles.noticeCopy}><Text style={styles.noticeTitle}>{item.title}</Text><Text style={styles.noticeBody}>{item.body}</Text><Text style={styles.date}>{new Date(item.createdAtUtc).toLocaleString('tr-TR')}</Text></View>
        {!item.readAtUtc ? <View style={styles.dot} /> : null}
      </Card>
    </Pressable>) : <Card><Empty icon="♧" title={matches.data?.length || messageRequests.data?.length ? 'İsteklerin hazır' : 'Şimdilik sessiz'} detail={matches.data?.length || messageRequests.data?.length ? 'Yukarıdan kabul ettiğinde sohbet açılır.' : 'Yeni bağlantı ve mesaj olduğunda burada göreceksin.'} /></Card>}
  </Screen>;
}

const styles = StyleSheet.create({
  screen: { paddingHorizontal: 20, paddingTop: 15 }, action: { fontSize: 10, color: colors.violet, fontWeight: '800' }, section: { marginTop: 10, marginBottom: 10, color: colors.ink, fontSize: 14, fontWeight: '900' },
  requestCard: { padding: 13, marginBottom: 9 }, person: { flexDirection: 'row', alignItems: 'center', gap: 11 }, personCopy: { flex: 1 }, personName: { fontSize: 13, color: colors.ink, fontWeight: '900' }, personDetail: { fontSize: 10, lineHeight: 15, color: colors.muted, marginTop: 3 }, buttons: { flexDirection: 'row', gap: 8, marginTop: 13 }, flex: { flex: 1 },
  notice: { padding: 13, marginBottom: 9, flexDirection: 'row', alignItems: 'flex-start', gap: 11 }, unread: { borderColor: '#DDD5FF', backgroundColor: '#FCFBFF' }, icon: { width: 39, height: 39, borderRadius: 13, backgroundColor: colors.paleViolet, alignItems: 'center', justifyContent: 'center' }, noticeCopy: { flex: 1 }, noticeTitle: { fontSize: 12, color: colors.ink, fontWeight: '800' }, noticeBody: { fontSize: 11, lineHeight: 16, color: colors.muted, marginTop: 3 }, date: { fontSize: 9, color: colors.faint, marginTop: 6 }, dot: { width: 7, height: 7, borderRadius: 4, backgroundColor: colors.coral, marginTop: 4 },
});
