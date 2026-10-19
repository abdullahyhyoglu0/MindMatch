import { useEffect, useRef, useState } from 'react';
import { ActivityIndicator, Alert, FlatList, KeyboardAvoidingView, Platform, Pressable, StyleSheet, Text, TextInput, View } from 'react-native';
import { router, useLocalSearchParams } from 'expo-router';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { ArrowLeft, ImagePlus, Mic, MoreHorizontal, Pause, Phone, Play, Send, Square, Trash2, Video, X } from 'lucide-react-native';
import * as ImagePicker from 'expo-image-picker';
import { AudioModule, RecordingPresets, setAudioModeAsync, useAudioPlayer, useAudioPlayerStatus, useAudioRecorder, useAudioRecorderState } from 'expo-audio';
import { useSafeAreaInsets } from 'react-native-safe-area-context';
import { chatApi, callApi, mediaApi } from '../../api/mindmatch';
import { Avatar, Card, Loading, ProtectedImage } from '../../components/ui';
import { colors } from '../../theme/tokens';
import { useSession } from '../../state/session';
import { connectMessages, getMessageHub } from '../../realtime/hub';
import { normalizeImageForUpload, uploadFailureMessage } from '../../utils/media';
import { API_URL, sessionTokens } from '../../api/client';
import type { Message } from '../../api/types';

let activeVoicePlayer: { pause: () => void } | null = null;

export default function ConversationScreen() {
  const { id, userId, name } = useLocalSearchParams<{ id: string; userId: string; name?: string }>();
  const me = useSession(x => x.user);
  const insets = useSafeAreaInsets();
  const qc = useQueryClient();
  const [text, setText] = useState('');
  const [recordingUri, setRecordingUri] = useState<string | null>(null);
  const [voiceDurationMs, setVoiceDurationMs] = useState(0);
  const [uploadingVoice, setUploadingVoice] = useState(false);
  const audioRecorder = useAudioRecorder({
    ...RecordingPresets.HIGH_QUALITY,
    directory: 'cache',
    bitRate: 64000,
    numberOfChannels: 1,
  });
  const recorderState = useAudioRecorderState(audioRecorder, 250);
  const recordingWasActive = useRef(false);
  const finalizingRecording = useRef(false);
  const listRef = useRef<FlatList<Message>>(null);
  const q = useQuery({ queryKey: ['messages', id], queryFn: () => chatApi.messages(id), refetchInterval: 15_000 });
  const messages = q.data?.items ?? [];

  useEffect(() => {
    let active = true;
    connectMessages().then(hub => {
      if (!hub || !active) return;
      const refresh = (message: { conversationId?: string }) => {
        if (!message?.conversationId || message.conversationId === id) qc.invalidateQueries({ queryKey: ['messages', id] });
      };
      hub.on('MessageCreated', refresh);
      hub.on('MessageDeleted', refresh);
      hub.on('MessageHidden', refresh);
    });
    return () => {
      active = false;
      getMessageHub()?.off('MessageCreated');
      getMessageHub()?.off('MessageDeleted');
      getMessageHub()?.off('MessageHidden');
    };
  }, [id, qc]);

  useEffect(() => {
    if (messages.length) listRef.current?.scrollToEnd({ animated: true });
  }, [messages.length]);

  useEffect(() => {
    if (recorderState.isRecording) {
      recordingWasActive.current = true;
      return;
    }
    if (!recordingWasActive.current || finalizingRecording.current) return;
    recordingWasActive.current = false;
    setRecordingUri(audioRecorder.uri ?? null);
    setVoiceDurationMs(recorderState.durationMillis);
    setAudioModeAsync({ allowsRecording: false, playsInSilentMode: true }).catch(() => {});
  }, [audioRecorder, recorderState.durationMillis, recorderState.isRecording]);

  const send = useMutation({
    mutationFn: () => chatApi.send(id, text.trim()),
    onSuccess: () => {
      setText('');
      qc.invalidateQueries({ queryKey: ['messages', id] });
      qc.invalidateQueries({ queryKey: ['conversations'] });
    },
    onError: error => Alert.alert('Mesaj gönderilemedi', error.message),
  });

  async function sendImage() {
    try {
      const picked = await ImagePicker.launchImageLibraryAsync({ mediaTypes: ['images'], quality: 0.9 });
      if (picked.canceled) return;
      const image = await normalizeImageForUpload(picked.assets[0].uri);
      const media = await mediaApi.upload(image.uri, image.fileName, image.mimeType, 2);
      try {
        await chatApi.sendMedia(id, media.id, 2);
      } catch (error) {
        await mediaApi.delete(media.id).catch(() => {});
        throw error;
      }
      await qc.invalidateQueries({ queryKey: ['messages', id] });
    } catch (error) {
      Alert.alert('Fotoğraf gönderilemedi', uploadFailureMessage(error));
    }
  }

  async function startVoiceRecording() {
    try {
      const permission = await AudioModule.requestRecordingPermissionsAsync();
      if (!permission.granted) {
        Alert.alert('Mikrofon izni gerekli', 'Sesli mesaj kaydetmek için iPhone ayarlarından MindMatch mikrofon iznini aç.');
        return;
      }
      setRecordingUri(null);
      setVoiceDurationMs(0);
      await setAudioModeAsync({ allowsRecording: true, playsInSilentMode: true });
      await audioRecorder.prepareToRecordAsync();
      audioRecorder.record({ forDuration: 120 });
    } catch (error) {
      await setAudioModeAsync({ allowsRecording: false, playsInSilentMode: true }).catch(() => {});
      Alert.alert('Kayıt başlatılamadı', error instanceof Error ? error.message : 'Mikrofon kullanılamadı.');
    }
  }

  async function stopVoiceRecording() {
    if (!recorderState.isRecording) return;
    finalizingRecording.current = true;
    try {
      setVoiceDurationMs(recorderState.durationMillis);
      await audioRecorder.stop();
      setRecordingUri(audioRecorder.uri ?? null);
    } catch (error) {
      Alert.alert('Kayıt tamamlanamadı', error instanceof Error ? error.message : 'Ses kaydı kaydedilemedi.');
    } finally {
      recordingWasActive.current = false;
      finalizingRecording.current = false;
      await setAudioModeAsync({ allowsRecording: false, playsInSilentMode: true }).catch(() => {});
    }
  }

  async function cancelVoiceRecording() {
    if (recorderState.isRecording) {
      finalizingRecording.current = true;
      await audioRecorder.stop().catch(() => {});
      finalizingRecording.current = false;
      recordingWasActive.current = false;
      await setAudioModeAsync({ allowsRecording: false, playsInSilentMode: true }).catch(() => {});
    }
    setRecordingUri(null);
    setVoiceDurationMs(0);
  }

  async function sendVoiceMessage() {
    if (!recordingUri || uploadingVoice) return;
    setUploadingVoice(true);
    let mediaId: string | undefined;
    try {
      const media = await mediaApi.upload(recordingUri, 'mindmatch-voice.m4a', 'audio/mp4', 3);
      mediaId = media.id;
      await chatApi.sendMedia(id, media.id, 3);
      setRecordingUri(null);
      setVoiceDurationMs(0);
      await Promise.all([
        qc.invalidateQueries({ queryKey: ['messages', id] }),
        qc.invalidateQueries({ queryKey: ['conversations'] }),
      ]);
    } catch (error) {
      if (mediaId) await mediaApi.delete(mediaId).catch(() => {});
      Alert.alert('Sesli mesaj gönderilemedi', uploadFailureMessage(error));
    } finally {
      setUploadingVoice(false);
    }
  }

  async function startCall(type: number) {
    try {
      const call = await callApi.start(id, type);
      router.push({ pathname: '/call/[id]', params: { id: call.id, type: String(type), name: name || 'Bağlantı' } });
    } catch (error) {
      Alert.alert('Arama başlatılamadı', error instanceof Error ? error.message : 'Bu görüşme başlatılamadı.');
    }
  }

  if (q.isLoading) return <View style={styles.center}><Loading label="Sohbet açılıyor" /></View>;

  return (
    <KeyboardAvoidingView style={styles.page} behavior={Platform.OS === 'ios' ? 'padding' : undefined} keyboardVerticalOffset={8}>
      <View style={[styles.header, { height: 67 + insets.top, paddingTop: insets.top }]}>
        <Pressable onPress={() => router.back()} style={styles.round}><ArrowLeft size={19} color={colors.ink} /></Pressable>
        <Avatar name={name || 'Bağlantı'} size={40} />
        <View style={styles.identity}><Text numberOfLines={1} style={styles.name}>{name || 'Bağlantı'}</Text><Text style={styles.online}>MindMatch sohbeti</Text></View>
        <Pressable accessibilityLabel="Sesli arama" onPress={() => startCall(1)} style={styles.round}><Phone size={17} color={colors.violet} /></Pressable>
        <Pressable accessibilityLabel="Görüntülü arama" onPress={() => startCall(2)} style={styles.round}><Video size={19} color={colors.violet} /></Pressable>
        <Pressable accessibilityLabel="Sohbet seçenekleri" onPress={() => router.push({ pathname: '/report', params: { userId, conversationId: id } })} style={styles.round}><MoreHorizontal size={19} color={colors.ink} /></Pressable>
      </View>
      <FlatList
        ref={listRef}
        data={messages}
        keyExtractor={message => message.id}
        contentContainerStyle={styles.messages}
        keyboardShouldPersistTaps="handled"
        onContentSizeChange={() => listRef.current?.scrollToEnd({ animated: false })}
        renderItem={({ item }) => {
          const mine = item.senderId === me?.userId;
          return (
            <View style={[styles.bubbleRow, mine ? styles.mineRow : styles.theirRow]}>
              <View style={[styles.bubble, mine ? styles.mineBubble : styles.theirBubble]}>
                {item.type === 2 && item.mediaAssetId ? <ProtectedImage mediaAssetId={item.mediaAssetId} style={styles.messageImage} /> : null}
                {item.type === 3 && item.mediaAssetId ? <VoiceMessage mediaAssetId={item.mediaAssetId} mine={mine} /> : null}
                {item.content ? <Text style={[styles.messageText, mine && styles.mineText]}>{item.content}</Text> : item.type === 2 ? <Text style={[styles.messageText, mine && styles.mineText]}>Fotoğraf</Text> : null}
                <Text style={[styles.time, mine && styles.mineTime]}>{new Date(item.createdAtUtc).toLocaleTimeString('tr-TR', { hour: '2-digit', minute: '2-digit' })}</Text>
              </View>
            </View>
          );
        }}
        ListEmptyComponent={<Card style={styles.empty}><Text style={styles.emptyTitle}>İlk merhaba senden gelsin</Text><Text style={styles.emptyText}>Bu sohbet yalnızca eşleştiğiniz kişiler arasında görünür.</Text></Card>}
      />
      <View style={styles.composer}>
        <Pressable accessibilityLabel="Fotoğraf ekle" onPress={sendImage} disabled={recorderState.isRecording || !!recordingUri || uploadingVoice} style={styles.attach}><ImagePlus color={colors.violet} size={20} /></Pressable>
        {recorderState.isRecording ? <View style={styles.voiceDraft}><View style={styles.recordingDot}/><Text style={styles.voiceDraftText}>Kaydediliyor · {formatDuration(recorderState.durationMillis)}</Text></View>
          : recordingUri ? <View style={styles.voiceDraft}><Mic color={colors.violet} size={16}/><Text style={styles.voiceDraftText}>Sesli mesaj · {formatDuration(voiceDurationMs)}</Text></View>
          : <TextInput value={text} onChangeText={setText} placeholder="Bir şeyler yaz…" placeholderTextColor={colors.faint} multiline maxLength={2000} style={styles.input} />}
        {recorderState.isRecording ? <>
          <Pressable accessibilityLabel="Kaydı iptal et" onPress={cancelVoiceRecording} style={styles.voiceAction}><X color={colors.muted} size={18}/></Pressable>
          <Pressable accessibilityLabel="Kaydı bitir" onPress={stopVoiceRecording} style={styles.voiceRecordStop}><Square color={colors.white} size={16} fill={colors.white}/></Pressable>
        </> : recordingUri ? <>
          <Pressable accessibilityLabel="Kaydı sil" onPress={cancelVoiceRecording} disabled={uploadingVoice} style={styles.voiceAction}><Trash2 color={colors.muted} size={18}/></Pressable>
          <Pressable accessibilityLabel="Sesli mesajı gönder" onPress={sendVoiceMessage} disabled={uploadingVoice} style={[styles.send, uploadingVoice && styles.disabled]}>{uploadingVoice ? <ActivityIndicator color={colors.white}/> : <Send color={colors.white} size={18}/>}</Pressable>
        </> : <>
          <Pressable accessibilityLabel="Sesli mesaj kaydet" onPress={startVoiceRecording} style={styles.attach}><Mic color={colors.violet} size={19}/></Pressable>
          <Pressable accessibilityLabel="Mesaj gönder" disabled={!text.trim() || send.isPending} onPress={() => send.mutate()} style={[styles.send, (!text.trim() || send.isPending) && styles.disabled]}><Send color={colors.white} size={18} /></Pressable>
        </>}
      </View>
    </KeyboardAvoidingView>
  );
}

function VoiceMessage({ mediaAssetId, mine }: { mediaAssetId: string; mine: boolean }) {
  const player = useAudioPlayer(null);
  const status = useAudioPlayerStatus(player);
  const loaded = useRef(false);
  const [loading, setLoading] = useState(false);
  const [failed, setFailed] = useState(false);

  useEffect(() => () => {
    if (activeVoicePlayer === player) activeVoicePlayer = null;
    player.pause();
  }, [player]);

  async function togglePlayback() {
    if (status.playing) {
      player.pause();
      if (activeVoicePlayer === player) activeVoicePlayer = null;
      return;
    }
    setLoading(true);
    setFailed(false);
    try {
      const token = await sessionTokens.access();
      if (!token) throw new Error('Oturum süresi dolmuş. Yeniden giriş yap.');
      await setAudioModeAsync({ allowsRecording: false, playsInSilentMode: true, interruptionMode: 'doNotMix' });
      if (!loaded.current) {
        player.replace({ uri: `${API_URL}/api/media/${mediaAssetId}`, headers: { Authorization: `Bearer ${token}` } });
        loaded.current = true;
      }
      if (activeVoicePlayer && activeVoicePlayer !== player) activeVoicePlayer.pause();
      activeVoicePlayer = player;
      if (status.didJustFinish) player.seekTo(0);
      player.play();
    } catch {
      setFailed(true);
      Alert.alert('Ses açılamadı', 'Sesli mesaj indirilemedi. İnternet bağlantını kontrol edip tekrar dene.');
    } finally {
      setLoading(false);
    }
  }

  const duration = status.duration > 0 ? status.duration * 1000 : 0;
  const position = status.currentTime * 1000;
  const label = duration ? `${formatDuration(position)} / ${formatDuration(duration)}` : failed ? 'Tekrar dene' : 'Sesli mesaj';
  return <View style={styles.voiceMessage}>
    <Pressable accessibilityLabel={status.playing ? 'Sesli mesajı duraklat' : 'Sesli mesajı dinle'} onPress={togglePlayback} style={[styles.voicePlay, mine && styles.voicePlayMine]}>
      {loading ? <ActivityIndicator size="small" color={mine ? colors.violet : colors.white}/> : status.playing ? <Pause color={mine ? colors.violet : colors.white} size={17}/> : <Play color={mine ? colors.violet : colors.white} size={17} fill={mine ? colors.violet : colors.white}/>}
    </Pressable>
    <View style={styles.voiceInfo}><View style={[styles.voiceTrack, mine && styles.voiceTrackMine]}><View style={[styles.voiceProgress, { width: `${duration ? Math.min(100, (position / duration) * 100) : 0}%` }]} /></View><Text style={[styles.voiceDuration, mine && styles.voiceDurationMine]}>{label}</Text></View>
  </View>;
}

function formatDuration(durationMs: number) {
  const seconds = Math.floor(Math.max(0, durationMs) / 1000);
  return `${Math.floor(seconds / 60)}:${String(seconds % 60).padStart(2, '0')}`;
}

const styles = StyleSheet.create({
  page: { flex: 1, backgroundColor: colors.canvas }, center: { flex: 1, backgroundColor: colors.canvas },
  header: { height: 67, backgroundColor: colors.white, borderBottomColor: colors.line, borderBottomWidth: 1, flexDirection: 'row', alignItems: 'center', paddingHorizontal: 12, gap: 8 },
  round: { width: 36, height: 36, borderRadius: 13, borderWidth: 1, borderColor: colors.line, alignItems: 'center', justifyContent: 'center' },
  identity: { flex: 1, minWidth: 0 }, name: { fontSize: 13, fontWeight: '900', color: colors.ink }, online: { fontSize: 9, color: colors.green, marginTop: 3 },
  messages: { paddingHorizontal: 15, paddingVertical: 16, gap: 9, flexGrow: 1, justifyContent: 'flex-end' },
  bubbleRow: { width: '100%', flexDirection: 'row' }, mineRow: { justifyContent: 'flex-end' }, theirRow: { justifyContent: 'flex-start' },
  bubble: { maxWidth: '83%', padding: 11, borderRadius: 17 }, mineBubble: { backgroundColor: colors.violet, borderBottomRightRadius: 5 },
  theirBubble: { backgroundColor: colors.white, borderWidth: 1, borderColor: colors.line, borderBottomLeftRadius: 5 },
  messageText: { fontSize: 13, color: colors.ink, lineHeight: 18 }, mineText: { color: colors.white },
  time: { alignSelf: 'flex-end', fontSize: 8, color: colors.faint, marginTop: 5 }, mineTime: { color: '#FFFFFFA8' },
  messageImage: { width: 205, height: 220, borderRadius: 12, marginBottom: 5 },
  empty: { alignSelf: 'center', marginVertical: 30, maxWidth: 275 }, emptyTitle: { fontSize: 13, color: colors.ink, fontWeight: '800', textAlign: 'center' },
  emptyText: { fontSize: 10, color: colors.muted, lineHeight: 15, textAlign: 'center', marginTop: 5 },
  composer: { flexDirection: 'row', alignItems: 'flex-end', gap: 8, paddingHorizontal: 12, paddingVertical: 10, borderTopColor: colors.line, borderTopWidth: 1, backgroundColor: colors.white },
  attach: { height: 43, width: 34, alignItems: 'center', justifyContent: 'center' },
  input: { flex: 1, minHeight: 43, maxHeight: 112, paddingHorizontal: 15, paddingVertical: 11, borderWidth: 1, borderColor: colors.line, borderRadius: 18, backgroundColor: colors.canvas, color: colors.ink, fontSize: 14 },
  voiceDraft: { flex: 1, minHeight: 43, flexDirection: 'row', alignItems: 'center', gap: 8, paddingHorizontal: 12, borderRadius: 16, backgroundColor: colors.paleViolet },
  voiceDraftText: { flex: 1, fontSize: 11, color: colors.ink, fontWeight: '700' },
  recordingDot: { width: 9, height: 9, borderRadius: 5, backgroundColor: colors.coral },
  voiceAction: { width: 34, height: 42, alignItems: 'center', justifyContent: 'center' },
  voiceRecordStop: { width: 42, height: 42, borderRadius: 15, backgroundColor: colors.coral, alignItems: 'center', justifyContent: 'center' },
  voiceMessage: { width: 205, flexDirection: 'row', alignItems: 'center', gap: 10, minHeight: 44 },
  voicePlay: { width: 34, height: 34, borderRadius: 17, backgroundColor: colors.violet, alignItems: 'center', justifyContent: 'center' },
  voicePlayMine: { backgroundColor: '#FFFFFF' },
  voiceInfo: { flex: 1, gap: 5 },
  voiceTrack: { height: 4, borderRadius: 3, backgroundColor: '#FFFFFF45', overflow: 'hidden' },
  voiceTrackMine: { backgroundColor: '#FFFFFF65' },
  voiceProgress: { height: 4, borderRadius: 3, backgroundColor: colors.violet },
  voiceDuration: { color: colors.white, fontSize: 9, fontWeight: '700' },
  voiceDurationMine: { color: '#FFFFFFD0' },
  send: { width: 43, height: 43, borderRadius: 15, backgroundColor: colors.violet, alignItems: 'center', justifyContent: 'center' }, disabled: { opacity: 0.45 },
});
