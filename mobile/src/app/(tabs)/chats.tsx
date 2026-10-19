import { useQueries, useQuery } from '@tanstack/react-query';
import { router } from 'expo-router';
import { MessageCircle, Search } from 'lucide-react-native';
import { FlatList, Pressable, StyleSheet, Text, View } from 'react-native';
import { chatApi } from '../../api/mindmatch';
import { Avatar, Card, Empty, Loading, Screen, Title } from '../../components/ui';
import { colors } from '../../theme/tokens';

export default function ChatsScreen(){
 const list=useQuery({queryKey:['conversations'],queryFn:chatApi.conversations,refetchInterval:30000});
 const people=useQueries({queries:(list.data||[]).map(c=>({queryKey:['profile',c.otherUserId],queryFn:()=>chatApi.profile(c.otherUserId)}))});
 if(list.isLoading)return <Screen><Loading label="Sohbetler yükleniyor"/></Screen>;
 return <Screen style={{paddingHorizontal:22,paddingTop:15}}><Title eyebrow="BAĞLANTILARIN" title="Sohbetler" subtitle="İyi hissettiren konuşmalar burada." right={<Pressable style={s.action} onPress={()=>router.push('/requests')}><MessageCircle size={18} color={colors.violet}/></Pressable>}/>
 <View style={s.search}><Search size={17} color={colors.faint}/><Text style={s.searchText}>Sohbetlerinde ara</Text></View>
 <FlatList data={list.data||[]} keyExtractor={x=>x.id} contentContainerStyle={{paddingBottom:30,flexGrow:1}} ItemSeparatorComponent={()=><View style={s.sep}/>} renderItem={({item,index})=>{const p=people[index]?.data;return <Pressable onPress={()=>router.push({pathname:'/conversation/[id]',params:{id:item.id,userId:item.otherUserId,name:p?.displayName||'Bağlantı'}})} style={({pressed})=>[s.row,pressed&&{opacity:.7}]}><Avatar name={p?.displayName||'MM'} size={54}/><View style={{flex:1}}><View style={s.nameLine}><Text style={s.name}>{p?.displayName||'MindMatch kullanıcısı'}</Text><Text style={s.time}>{item.lastMessageAtUtc?new Date(item.lastMessageAtUtc).toLocaleDateString('tr-TR',{day:'numeric',month:'short'}):''}</Text></View><Text numberOfLines={1} style={s.last}>Sohbete devam et</Text></View><View style={s.chevron}><Text style={{color:colors.violet,fontSize:20}}>›</Text></View></Pressable>}} ListEmptyComponent={<Card style={{marginTop:18}}><Empty icon="♡" title="İlk sohbetine yer aç" detail="Keşfet bölümünden biriyle eşleştiğinde sohbetin burada görünür." action={<Pressable onPress={()=>router.replace('/(tabs)')}><Text style={s.link}>Kişileri keşfet →</Text></Pressable>}/></Card>} />
 </Screen>;
}
const s=StyleSheet.create({action:{width:42,height:42,borderRadius:15,borderWidth:1,borderColor:colors.line,alignItems:'center',justifyContent:'center'},search:{height:46,borderRadius:14,backgroundColor:colors.white,borderWidth:1,borderColor:colors.line,flexDirection:'row',alignItems:'center',gap:10,paddingHorizontal:14,marginBottom:18},searchText:{fontSize:12,color:colors.faint},row:{flexDirection:'row',alignItems:'center',gap:12,paddingVertical:13},sep:{height:1,backgroundColor:colors.line,marginLeft:66},nameLine:{flexDirection:'row',justifyContent:'space-between',alignItems:'center'},name:{fontSize:13,fontWeight:'800',color:colors.ink},time:{fontSize:9,color:colors.faint},last:{fontSize:11,color:colors.muted,marginTop:5},chevron:{width:28,height:28,borderRadius:10,backgroundColor:colors.paleViolet,alignItems:'center',justifyContent:'center'},link:{marginTop:13,color:colors.violet,fontSize:12,fontWeight:'800'}});

