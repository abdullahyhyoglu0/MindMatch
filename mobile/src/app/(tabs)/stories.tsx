import { useState } from 'react';
import { Alert, Pressable, StyleSheet, Text, View } from 'react-native';
import * as ImagePicker from 'expo-image-picker';
import { useQueries, useQuery, useQueryClient } from '@tanstack/react-query';
import { CirclePlus, Eye, ImagePlus, Play, RefreshCw, Sparkles } from 'lucide-react-native';
import { router } from 'expo-router';
import { mediaApi, profileApi, storyApi } from '../../api/mindmatch';
import { Avatar, Button, Card, Empty, Loading, ProtectedImage, Screen } from '../../components/ui';
import { colors } from '../../theme/tokens';
import { normalizeImageForUpload, uploadFailureMessage } from '../../utils/media';
import { Field } from '../../components/ui';

export default function StoriesScreen() {
 const qc=useQueryClient(); const [uploading,setUploading]=useState(false); const [caption,setCaption]=useState('');
 const feed=useQuery({queryKey:['stories'],queryFn:storyApi.feed}); const people=useQueries({queries:(feed.data||[]).map(story=>({queryKey:['profile',story.userId],queryFn:()=>profileApi.get(story.userId)}))}); const mine=useQuery({queryKey:['my-stories'],queryFn:storyApi.mine});
 async function addStory(){
   setUploading(true);
   try {
     const result=await ImagePicker.launchImageLibraryAsync({mediaTypes:['images','videos'],quality:.82,videoMaxDuration:30,videoExportPreset:ImagePicker.VideoExportPreset.H264_1280x720});
     if(result.canceled)return;
     const asset=result.assets[0];const isVideo=asset.type==='video';
     const normalized=isVideo?{uri:asset.uri,fileName:'mindmatch-story.mp4',mimeType:'video/mp4'}:await normalizeImageForUpload(asset.uri);
     const media=await mediaApi.upload(normalized.uri,normalized.fileName,normalized.mimeType,isVideo?5:4);
     try{await storyApi.create(media.id,isVideo?2:1,caption.trim()||undefined);}
     catch(error){await mediaApi.delete(media.id).catch(()=>{});throw error;}
     setCaption('');
     await Promise.all([qc.invalidateQueries({queryKey:['stories']}),qc.invalidateQueries({queryKey:['my-stories']})]);
     Alert.alert('Hikâyen yayında','Paylaşımın 24 saat boyunca görünür.');
   }catch(error){Alert.alert('Hikâye paylaşılamadı',uploadFailureMessage(error));}
   finally{setUploading(false);}
 }
 async function openStory(item:{id:string;userId:string}){router.push({pathname:'/story/[id]',params:{id:item.id,userId:item.userId}});}
 if(feed.isLoading)return <Screen><Loading label="Hikâyeler yükleniyor"/></Screen>;
 return <Screen scroll><View style={styles.top}><View><Text style={styles.kicker}>KÜÇÜK ANLAR, YENİ BAĞLAR</Text><Text style={styles.title}>Hikâyeler</Text></View><Pressable style={styles.refresh} onPress={()=>feed.refetch()}><RefreshCw size={17} color={colors.violet}/></Pressable></View>
 <Card style={styles.ownCard}><View style={styles.ownHead}><View style={styles.addAvatar}><Avatar name="Sen" size={58}/><View style={styles.plus}><CirclePlus size={19} color={colors.white} fill={colors.violet}/></View></View><View style={{flex:1}}><Text style={styles.ownTitle}>Günün nasıl geçiyor?</Text><Text style={styles.ownCopy}>{mine.data?.length?`${mine.data.length} aktif hikâyen var`:'Bir anını paylaş, 24 saat kalsın.'}</Text></View></View><Field label="Hikâyene bir not ekle" value={caption} onChangeText={setCaption} placeholder="İstersen kısa bir şey yaz…" maxLength={180} style={styles.captionInput}/><Button title="Hikâye paylaş" icon={<ImagePlus size={17} color={colors.white}/>} onPress={addStory} loading={uploading} style={{marginTop:16}} /></Card>
 <View style={styles.tip}><Sparkles size={15} color={colors.violet}/><Text style={styles.tipText}>Hikâyeler yalnızca erişim iznin olan kişilerle paylaşılır.</Text></View>
 <View style={styles.feedHeader}><Text style={styles.sectionTitle}>Bağlantılarından</Text><Text style={styles.count}>{feed.data?.length||0}</Text></View>
 {feed.data?.length?<View style={styles.grid}>{feed.data.map((story,i)=><Pressable key={story.id} onPress={()=>openStory(story)} style={[styles.storyTile,i%3===1&&styles.storyTileOffset]}><View style={styles.tileMedia}>{story.mediaAssetId?<ProtectedImage mediaAssetId={story.mediaAssetId} style={StyleSheet.absoluteFill}/>:<View style={styles.tileFallback}><Text style={styles.tileInitial}>{String(story.userId).slice(0,1).toUpperCase()}</Text></View>}<View style={[styles.storyRing,story.hasViewed&&styles.storyRingSeen]}><Avatar name={people[i]?.data?.displayName||'MindMatch'} size={42}/></View>{story.mediaType===2?<View style={styles.playBadge}><Play size={12} color={colors.white} fill={colors.white}/></View>:null}<View style={styles.tileCaption}><Text numberOfLines={2} style={styles.caption}>{story.caption||'Bir an paylaştı'}</Text><View style={styles.viewer}><Eye size={10} color="#fff"/><Text style={styles.viewerText}>{story.hasViewed?'Görüldü':'Yeni'}</Text></View></View></View><Text style={styles.tileUser}>{people[i]?.data?.displayName||'MindMatch kullanıcısı'}</Text></Pressable>)}</View>:<Card style={{marginTop:12}}><Empty icon="◉" title="Henüz hikâye yok" detail="Bağlantıların bir an paylaştığında burada görünecek."/></Card>}
 </Screen>;
}
const styles=StyleSheet.create({top:{flexDirection:'row',alignItems:'center',justifyContent:'space-between',marginBottom:18},kicker:{fontSize:9,color:colors.violet,fontWeight:'800',letterSpacing:1.4},title:{fontSize:30,fontWeight:'900',letterSpacing:-1.2,color:colors.ink,marginTop:5},refresh:{width:42,height:42,borderRadius:15,alignItems:'center',justifyContent:'center',borderWidth:1,borderColor:colors.line,backgroundColor:colors.white},ownCard:{padding:17},ownHead:{flexDirection:'row',alignItems:'center',gap:13},addAvatar:{position:'relative'},plus:{position:'absolute',bottom:-1,right:-2,borderColor:colors.white,borderWidth:2,borderRadius:12},ownTitle:{fontWeight:'800',color:colors.ink,fontSize:14},ownCopy:{fontSize:11,color:colors.muted,marginTop:4},captionInput:{height:43,fontSize:12,marginTop:14},tip:{flexDirection:'row',alignItems:'center',gap:8,marginTop:15,paddingHorizontal:3},tipText:{fontSize:10,color:colors.muted,flex:1},feedHeader:{flexDirection:'row',justifyContent:'space-between',alignItems:'center',marginTop:27,marginBottom:14},sectionTitle:{fontSize:16,color:colors.ink,fontWeight:'800'},count:{fontSize:11,color:colors.muted},grid:{flexDirection:'row',flexWrap:'wrap',justifyContent:'space-between',gap:13},storyTile:{width:'31%',marginBottom:4},storyTileOffset:{marginTop:12},tileMedia:{height:166,borderRadius:20,overflow:'hidden',backgroundColor:'#DCD5FF',justifyContent:'flex-end'},tileFallback:{...StyleSheet.absoluteFill,backgroundColor:'#DCD5FF',alignItems:'center',justifyContent:'center'},tileInitial:{fontSize:70,color:'#FFFFFF65',fontWeight:'900'},storyRing:{position:'absolute',top:9,left:9,padding:3,borderWidth:2,borderColor:colors.coral,borderRadius:25,backgroundColor:colors.white},storyRingSeen:{borderColor:'#C8C5CE'},playBadge:{position:'absolute',top:14,right:12,width:24,height:24,borderRadius:12,alignItems:'center',justifyContent:'center',backgroundColor:'#17132988'},tileCaption:{paddingHorizontal:8,paddingTop:30,paddingBottom:8,backgroundColor:'#17132988'},caption:{fontSize:10,color:colors.white,fontWeight:'700',lineHeight:14},viewer:{flexDirection:'row',alignItems:'center',gap:4,marginTop:5},viewerText:{fontSize:8,color:'#FFFFFFC8'},tileUser:{fontSize:10,color:colors.ink,fontWeight:'700',marginTop:6}});

