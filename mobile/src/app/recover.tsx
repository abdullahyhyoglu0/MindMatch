import { useState } from 'react';
import { Alert, Pressable, StyleSheet, Text } from 'react-native';
import { router } from 'expo-router';
import { useMutation } from '@tanstack/react-query';
import { ArrowLeft, Mail } from 'lucide-react-native';
import { accountApi } from '../api/mindmatch';
import { Button, Field, Screen, Title } from '../components/ui';
import { colors } from '../theme/tokens';
export default function RecoverScreen(){const [email,setEmail]=useState('');const m=useMutation({mutationFn:accountApi.forgotPassword,onSuccess:()=>Alert.alert('İstek alındı','Bu e-posta bir hesaba bağlıysa sıfırlama e-postası gönderilir.'),onError:e=>Alert.alert('İstek gönderilemedi',e.message)});return <Screen scroll style={{paddingHorizontal:22,paddingTop:12}}><Pressable onPress={()=>router.back()} style={s.back}><ArrowLeft size={19} color={colors.ink}/></Pressable><Title eyebrow="HESAP ERİŞİMİ" title="Parolanı yenile" subtitle="Hesabındaki e-posta adresini yaz; sıfırlama bağlantısı gönderelim."/><Field label="E-posta" value={email} onChangeText={setEmail} autoCapitalize="none" keyboardType="email-address" placeholder="sen@ornek.com"/><Button title="Sıfırlama isteği gönder" icon={<Mail color={colors.white} size={17}/>} onPress={()=>m.mutate(email.trim())} loading={m.isPending} style={{marginTop:17}}/><Text style={s.note}>Sıfırlama e-postasının teslimi sunucudaki SMTP ayarlarına bağlıdır.</Text></Screen>}
const s=StyleSheet.create({back:{width:40,height:40,borderRadius:14,alignItems:'center',justifyContent:'center',backgroundColor:colors.white,borderWidth:1,borderColor:colors.line,marginBottom:20},note:{fontSize:10,color:colors.muted,lineHeight:15,marginTop:14,textAlign:'center'}});

