import { useEffect, useRef, useState } from 'react';
import { Tabs, router } from 'expo-router';
import { Compass, MessageCircle, Sparkles, UserRound, Bell } from 'lucide-react-native';
import { Alert, Modal, Pressable, StyleSheet, Text, View } from 'react-native';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import * as Haptics from 'expo-haptics';
import { useSession } from '../../state/session';
import { colors } from '../../theme/tokens';
import { callApi, chatApi, matchApi, notificationApi } from '../../api/mindmatch';
import { connectMessages, stopMessages } from '../../realtime/hub';

const icons = { index: Compass, stories: Sparkles, chats: MessageCircle, notifications: Bell, profile: UserRound } as const;
type IncomingPrompt = { kind: 'match'; id: string; userId: string } | { kind: 'message'; conversationId: string; userId: string };

export default function TabLayout() {
  const user = useSession(s => s.user); const queryClient = useQueryClient();
  const { data: notificationPage } = useQuery({ queryKey:['notifications'], queryFn:notificationApi.list, enabled:!!user, refetchInterval:10_000 });
  const { data: incomingMatches } = useQuery({ queryKey:['matches','requests'], queryFn:matchApi.requests, enabled:!!user, refetchInterval:2_000 });
  const { data: incomingMessages } = useQuery({ queryKey:['message-requests'], queryFn:chatApi.incoming, enabled:!!user, refetchInterval:2_000 });
  const [prompt, setPrompt] = useState<IncomingPrompt | null>(null);
  const [promptBusy, setPromptBusy] = useState(false);
  const [promptError, setPromptError] = useState('');
  const seenRequests = useRef(new Set<string>());
  useEffect(() => {
    if (prompt || !user) return;
    const match = incomingMatches?.find(request => !seenRequests.current.has(`match:${request.id}`));
    if (match) {
      seenRequests.current.add(`match:${match.id}`);
      setPrompt({ kind: 'match', id: match.id, userId: match.requesterId });
      return;
    }
    const message = incomingMessages?.find(request => !seenRequests.current.has(`message:${request.conversationId}`));
    if (message) {
      seenRequests.current.add(`message:${message.conversationId}`);
      setPrompt({ kind: 'message', conversationId: message.conversationId, userId: message.senderId });
    }
  }, [prompt, incomingMatches, incomingMessages, user]);

  async function answerRequest(accept: boolean) {
    if (!prompt || promptBusy) return;
    const activePrompt = prompt;
    setPromptBusy(true);
    setPromptError('');
    try {
      if (activePrompt.kind === 'match') {
        if (accept) {
          const match = await matchApi.accept(activePrompt.id);
          await queryClient.invalidateQueries({ queryKey: ['conversations'] });
          const conversations = await queryClient.fetchQuery({ queryKey: ['conversations'], queryFn: chatApi.conversations });
          const conversation = conversations.find(item => item.otherUserId === match.requesterId);
          if (conversation) router.push({ pathname: '/conversation/[id]', params: { id: conversation.id, userId: activePrompt.userId } });
        } else await matchApi.reject(activePrompt.id);
      } else {
        if (accept) {
          await chatApi.acceptMessageRequest(activePrompt.conversationId);
          router.push({ pathname: '/conversation/[id]', params: { id: activePrompt.conversationId, userId: activePrompt.userId } });
        } else await chatApi.rejectMessageRequest(activePrompt.conversationId);
      }
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: ['matches'] }),
        queryClient.invalidateQueries({ queryKey: ['message-requests'] }),
        queryClient.invalidateQueries({ queryKey: ['notifications'] }),
      ]);
      setPrompt(null);
    } catch (error) {
      setPromptError(error instanceof Error ? error.message : 'İstek yanıtlanamadı. Tekrar dene.');
    } finally {
      setPromptBusy(false);
    }
  }

  useEffect(() => {
    if (!user) { router.replace('/'); return; }
    let active = true;
    connectMessages().then(hub => {
      if (!hub || !active) return;
      const refresh = () => {
        Haptics.notificationAsync(Haptics.NotificationFeedbackType.Success).catch(()=>{});
        queryClient.invalidateQueries({ queryKey: ['notifications'] });
        queryClient.invalidateQueries({ queryKey: ['matches', 'requests'] });
        queryClient.invalidateQueries({ queryKey: ['message-requests'] });
        queryClient.invalidateQueries({ queryKey: ['conversations'] });
      };
      ['NotificationReceived','MatchCreated','MatchAccepted','MessageRequestReceived','MessageCreated','ConversationUpdated'].forEach(e=>hub.on(e,refresh));
      const incoming = (call: { id?: string; type?: number }) => {
        if (!call.id) return;
        Alert.alert('Gelen arama', 'Bir MindMatch kullanıcısı seni arıyor.', [
          { text: 'Reddet', style: 'cancel', onPress: () => callApi.reject(call.id!).catch(() => {}) },
          { text: 'Yanıtla', onPress: () => router.push({ pathname: '/call/[id]', params: { id: call.id!, type: String(call.type || 1), incoming: 'true' } }) }
        ]);
      };
      hub.on('CallIncoming', incoming);
    });
    return () => { active=false; stopMessages(); };
  }, [user, queryClient]);
  const unread=notificationPage?.items?.filter(x=>!x.readAtUtc).length ?? 0;
  return <>
    <Tabs screenOptions={({route})=>({headerShown:false,tabBarShowLabel:false,tabBarActiveTintColor:colors.violet,tabBarInactiveTintColor:colors.faint,tabBarStyle:styles.bar,tabBarItemStyle:styles.item,tabBarIcon:({focused,color})=>{const Icon=icons[route.name as keyof typeof icons];return <View style={[styles.iconWrap,focused&&styles.iconActive]}><Icon size={21} color={color} strokeWidth={focused?2.5:2}/>{route.name==='notifications'&&unread>0?<View style={styles.dot}/>:null}<Text style={[styles.label,{color, fontWeight:focused?'800':'600'}]}>{({index:'Keşfet',stories:'Hikâye',chats:'Sohbet',notifications:'Bildirim',profile:'Profil'} as Record<string,string>)[route.name]}</Text></View>}})}>
    <Tabs.Screen name="index" options={{title:'Keşfet'}}/><Tabs.Screen name="stories" options={{title:'Hikâyeler'}}/><Tabs.Screen name="chats" options={{title:'Sohbetler'}}/><Tabs.Screen name="notifications" options={{title:'Bildirimler'}}/><Tabs.Screen name="profile" options={{title:'Profil'}}/>
    </Tabs>
    <Modal visible={Boolean(prompt)} transparent animationType="fade" onRequestClose={() => setPrompt(null)}>
      <View style={styles.promptBackdrop}>
        <View style={styles.promptCard}>
          <View style={styles.promptIcon}><Bell color={colors.violet} size={21}/></View>
          <Text style={styles.promptEyebrow}>YENİ İSTEK</Text>
          <Text style={styles.promptTitle}>{prompt?.kind === 'match' ? 'Eşleşme isteği geldi' : 'Mesaj isteği geldi'}</Text>
          <Text style={styles.promptBody}>{prompt?.kind === 'match' ? 'Bir kullanıcı seninle eşleşmek istiyor. İstersen profilini incele, sonra yanıtla.' : 'Sana yeni bir mesaj isteği gönderildi.'}</Text>
          {promptError ? <Text style={styles.promptError}>{promptError}</Text> : null}
          <Pressable accessibilityRole="button" disabled={promptBusy} onPress={() => { if (prompt) router.push({ pathname: '/user/[id]', params: { id: prompt.userId } }); setPrompt(null); }} style={styles.profileAction}><Text style={styles.profileActionText}>Profili görüntüle</Text></Pressable>
          <View style={styles.promptButtons}>
            <Pressable accessibilityRole="button" disabled={promptBusy} onPress={() => answerRequest(false)} style={[styles.promptButton, styles.rejectButton]}><Text style={styles.rejectText}>{promptBusy ? 'Bekle…' : 'Reddet'}</Text></Pressable>
            <Pressable accessibilityRole="button" disabled={promptBusy} onPress={() => answerRequest(true)} style={[styles.promptButton, styles.acceptButton]}><Text style={styles.acceptText}>{promptBusy ? 'Bekle…' : 'Kabul et'}</Text></Pressable>
          </View>
          <Pressable accessibilityRole="button" disabled={promptBusy} onPress={() => setPrompt(null)} style={styles.laterAction}><Text style={styles.laterText}>Daha sonra yanıtla</Text></Pressable>
        </View>
      </View>
    </Modal>
  </>;
}
const styles=StyleSheet.create({bar:{height:78,paddingTop:9,paddingBottom:9,backgroundColor:colors.white,borderTopColor:colors.line,borderTopWidth:1,elevation:16},item:{height:58},iconWrap:{minWidth:58,height:54,alignItems:'center',justifyContent:'center',gap:3,borderRadius:18,position:'relative'},iconActive:{backgroundColor:colors.paleViolet},label:{fontSize:9},dot:{position:'absolute',top:3,right:15,width:8,height:8,borderRadius:4,backgroundColor:colors.coral,borderWidth:1,borderColor:colors.white},promptBackdrop:{flex:1,backgroundColor:'#17132988',alignItems:'center',justifyContent:'center',padding:22},promptCard:{width:'100%',maxWidth:390,padding:22,backgroundColor:colors.white,borderRadius:26,alignItems:'center',shadowColor:'#22164A',shadowOpacity:0.18,shadowRadius:24,elevation:18},promptIcon:{width:50,height:50,borderRadius:18,backgroundColor:colors.paleViolet,alignItems:'center',justifyContent:'center',marginBottom:12},promptEyebrow:{fontSize:9,letterSpacing:1.5,color:colors.violet,fontWeight:'900'},promptTitle:{fontSize:21,color:colors.ink,fontWeight:'900',textAlign:'center',marginTop:7},promptBody:{fontSize:13,lineHeight:19,color:colors.muted,textAlign:'center',marginTop:8,marginBottom:15},promptError:{fontSize:12,color:colors.red,textAlign:'center',marginBottom:10},profileAction:{paddingVertical:9,paddingHorizontal:15},profileActionText:{color:colors.violet,fontSize:12,fontWeight:'800'},promptButtons:{flexDirection:'row',gap:10,width:'100%',marginTop:12},promptButton:{flex:1,minHeight:48,borderRadius:16,alignItems:'center',justifyContent:'center'},rejectButton:{borderWidth:1,borderColor:colors.line,backgroundColor:colors.white},acceptButton:{backgroundColor:colors.violet},rejectText:{color:colors.ink,fontSize:13,fontWeight:'800'},acceptText:{color:colors.white,fontSize:13,fontWeight:'800'},laterAction:{paddingVertical:14,paddingHorizontal:10},laterText:{color:colors.muted,fontSize:11,fontWeight:'700'}});

