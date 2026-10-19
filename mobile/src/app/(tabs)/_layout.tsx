import { useEffect } from 'react';
import { Tabs, router } from 'expo-router';
import { Compass, MessageCircle, Sparkles, UserRound, Bell } from 'lucide-react-native';
import { Alert, StyleSheet, Text, View } from 'react-native';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import * as Haptics from 'expo-haptics';
import { useSession } from '../../state/session';
import { colors } from '../../theme/tokens';
import { callApi, notificationApi } from '../../api/mindmatch';
import { connectMessages, stopMessages } from '../../realtime/hub';

const icons = { index: Compass, stories: Sparkles, chats: MessageCircle, notifications: Bell, profile: UserRound } as const;
export default function TabLayout() {
  const user = useSession(s => s.user); const queryClient = useQueryClient();
  const { data: notificationPage } = useQuery({ queryKey:['notifications'], queryFn:notificationApi.list, enabled:!!user, refetchInterval:60_000 });
  useEffect(() => {
    if (!user) { router.replace('/'); return; }
    let active = true;
    connectMessages().then(hub => {
      if (!hub || !active) return;
      const refresh = () => { Haptics.notificationAsync(Haptics.NotificationFeedbackType.Success).catch(()=>{}); queryClient.invalidateQueries(); };
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
  return <Tabs screenOptions={({route})=>({headerShown:false,tabBarShowLabel:false,tabBarActiveTintColor:colors.violet,tabBarInactiveTintColor:colors.faint,tabBarStyle:styles.bar,tabBarItemStyle:styles.item,tabBarIcon:({focused,color})=>{const Icon=icons[route.name as keyof typeof icons];return <View style={[styles.iconWrap,focused&&styles.iconActive]}><Icon size={21} color={color} strokeWidth={focused?2.5:2}/>{route.name==='notifications'&&unread>0?<View style={styles.dot}/>:null}<Text style={[styles.label,{color, fontWeight:focused?'800':'600'}]}>{({index:'Keşfet',stories:'Hikâye',chats:'Sohbet',notifications:'Bildirim',profile:'Profil'} as Record<string,string>)[route.name]}</Text></View>}})}>
    <Tabs.Screen name="index" options={{title:'Keşfet'}}/><Tabs.Screen name="stories" options={{title:'Hikâyeler'}}/><Tabs.Screen name="chats" options={{title:'Sohbetler'}}/><Tabs.Screen name="notifications" options={{title:'Bildirimler'}}/><Tabs.Screen name="profile" options={{title:'Profil'}}/>
  </Tabs>;
}
const styles=StyleSheet.create({bar:{height:78,paddingTop:9,paddingBottom:9,backgroundColor:colors.white,borderTopColor:colors.line,borderTopWidth:1,elevation:16},item:{height:58},iconWrap:{minWidth:58,height:54,alignItems:'center',justifyContent:'center',gap:3,borderRadius:18,position:'relative'},iconActive:{backgroundColor:colors.paleViolet},label:{fontSize:9},dot:{position:'absolute',top:3,right:15,width:8,height:8,borderRadius:4,backgroundColor:colors.coral,borderWidth:1,borderColor:colors.white}});

