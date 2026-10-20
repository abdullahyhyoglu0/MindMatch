import { useEffect, useMemo, useRef, useState } from 'react';
import { ActivityIndicator, Alert, FlatList, KeyboardAvoidingView, Modal, Platform, Pressable, StyleSheet, Text, TextInput, View } from 'react-native';
import { router, useLocalSearchParams } from 'expo-router';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { ArrowLeft, Check, CheckCheck, Eye, EyeOff, Flag, ImagePlus, Lock, MessageCircle, Mic, MoreHorizontal, Pause, Phone, Play, Send, Shield, Sparkles, Square, Trash2, Video, X } from 'lucide-react-native';
import * as ImagePicker from 'expo-image-picker';
import * as Haptics from 'expo-haptics';
import { AudioModule, RecordingPresets, setAudioModeAsync, useAudioPlayer, useAudioPlayerStatus, useAudioRecorder, useAudioRecorderState } from 'expo-audio';
import { useSafeAreaInsets } from 'react-native-safe-area-context';
import { blockApi, chatApi, callApi, matchApi, mediaApi, presenceApi } from '../../api/mindmatch';
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
  const [uploadingImage, setUploadingImage] = useState(false);
  const [imageUploadStage, setImageUploadStage] = useState('');
  const [imageUploadError, setImageUploadError] = useState('');
  const [uploadingVoice, setUploadingVoice] = useState(false);
  const [selectedMessage, setSelectedMessage] = useState<Message | null>(null);
  const [deleteAllConfirmVisible, setDeleteAllConfirmVisible] = useState(false);
  const [chatOptionsVisible, setChatOptionsVisible] = useState(false);
  const [blockConfirmVisible, setBlockConfirmVisible] = useState(false);
  const [lightboxMediaId, setLightboxMediaId] = useState<string | null>(null);
  const [revealedPhotos, setRevealedPhotos] = useState<Set<string>>(new Set());

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
  const matches = useQuery({ queryKey: ['matches'], queryFn: matchApi.list });
  const messages = useMemo(() => q.data?.items ?? [], [q.data?.items]);
  const profile = useQuery({ queryKey: ['profile', userId], queryFn: () => chatApi.profile(userId), enabled: Boolean(userId) });
  const presence = useQuery({ queryKey: ['presence', userId], queryFn: () => presenceApi.get(userId), enabled: Boolean(userId), refetchInterval: 20_000 });
  const readAttempts = useRef(new Set<string>());

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
      hub.on('MessageStatusChanged', refresh);
    });
    return () => {
      active = false;
      getMessageHub()?.off('MessageCreated');
      getMessageHub()?.off('MessageDeleted');
      getMessageHub()?.off('MessageHidden');
      getMessageHub()?.off('MessageStatusChanged');
    };
  }, [id, qc]);

  useEffect(() => {
    if (messages.length) listRef.current?.scrollToEnd({ animated: true });
  }, [messages.length]);

  useEffect(() => { readAttempts.current.clear(); }, [id]);
  useEffect(() => {
    if (!me?.userId) return;
    for (const message of messages) {
      if (message.senderId === me.userId || message.status >= 3 || message.isDeletedForEveryone || readAttempts.current.has(message.id)) continue;
      readAttempts.current.add(message.id);
      chatApi.markDelivered(message.id)
        .then(() => chatApi.markRead(message.id))
        .then(() => qc.invalidateQueries({ queryKey: ['messages', id] }))
        .catch(() => readAttempts.current.delete(message.id));
    }
  }, [messages, me?.userId, id, qc]);

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

  async function handleSendImage(asBlurred: boolean) {
    if (uploadingImage) return;
    setUploadingImage(true);
    setImageUploadStage('Fotoğraf seçiliyor…');
    setImageUploadError('');
    try {
      const picked = await ImagePicker.launchImageLibraryAsync({
        mediaTypes: ['images'],
        quality: 0.9,
        preferredAssetRepresentationMode: ImagePicker.UIImagePickerPreferredAssetRepresentationMode.Compatible,
      });
      if (picked.canceled) return;
      setImageUploadStage('Fotoğraf hazırlanıyor…');
      const image = await normalizeImageForUpload(picked.assets[0].uri);
      setImageUploadStage('Sunucuya yükleniyor…');
      const media = await mediaApi.upload(image.uri, image.fileName, image.mimeType, 2);
      try {
        setImageUploadStage('Mesaj gönderiliyor…');
        const contentTag = asBlurred ? '[blur] Gizli fotoğraf' : undefined;
        await chatApi.sendMedia(id, media.id, 2, contentTag);
      } catch (error) {
        await mediaApi.delete(media.id).catch(() => {});
        throw error;
      }
      await Promise.all([
        qc.invalidateQueries({ queryKey: ['messages', id] }),
        qc.invalidateQueries({ queryKey: ['conversations'] }),
      ]);
    } catch (error) {
      const message = uploadFailureMessage(error);
      setImageUploadError(message);
      if (Platform.OS !== 'web') Alert.alert('Fotoğraf gönderilemedi', message);
    } finally {
      setUploadingImage(false);
      setImageUploadStage('');
    }
  }

  function choosePhotoOptions() {
    Alert.alert('Fotoğraf Gönder', 'Nasıl göndermek istersin?', [
      { text: '👁️ Gizli / Blurlu Gönder', onPress: () => handleSendImage(true) },
      { text: '📷 Normal Gönder', onPress: () => handleSendImage(false) },
      { text: 'Vazgeç', style: 'cancel' },
    ]);
  }

  function toggleRevealPhoto(messageId: string, mediaAssetId: string) {
    Haptics.impactAsync(Haptics.ImpactFeedbackStyle.Light).catch(() => {});
    setRevealedPhotos(prev => {
      const next = new Set(prev);
      if (next.has(messageId)) next.delete(messageId);
      else next.add(messageId);
      return next;
    });
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
      const webRecording = Platform.OS === 'web';
      const media = await mediaApi.upload(recordingUri, webRecording ? 'mindmatch-voice.webm' : 'mindmatch-voice.m4a', webRecording ? 'audio/webm' : 'audio/mp4', 3);
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

  async function deleteSelectedMessage(forEveryone: boolean) {
    if (!selectedMessage) return;
    const selected = selectedMessage;
    setSelectedMessage(null);
    setDeleteAllConfirmVisible(false);
    try {
      if (forEveryone) await chatApi.deleteMessageForEveryone(selected.id);
      else await chatApi.deleteMessage(selected.id);
      await qc.invalidateQueries({ queryKey: ['messages', id] });
    } catch (error) {
      Alert.alert('Mesaj silinemedi', error instanceof Error ? error.message : 'Tekrar dene.');
    }
  }

  function openChatOptions() {
    setChatOptionsVisible(true);
  }

  async function confirmBlock() {
    if (!userId) return;
    setBlockConfirmVisible(false);
    try { await blockApi.block(userId); await qc.invalidateQueries(); router.back(); }
    catch (error) { Alert.alert('Engellenemedi', error instanceof Error ? error.message : 'Tekrar dene.'); }
  }

  function hideConversation() {
    setChatOptionsVisible(false);
    Alert.alert('Sohbet gizlensin mi?', 'Sohbet yalnızca senin listeninden kaldırılır. Yeni mesaj geldiğinde yeniden görünür.', [
      { text: 'Vazgeç', style: 'cancel' },
      { text: 'Gizle', style: 'destructive', onPress: async () => {
        try { await chatApi.hideConversation(id); await qc.invalidateQueries({ queryKey: ['conversations'] }); router.replace('/(tabs)/chats'); }
        catch (error) { Alert.alert('Sohbet gizlenemedi', error instanceof Error ? error.message : 'Tekrar dene.'); }
      } },
    ]);
  }

  function endMatch() {
    const match = matches.data?.find(item => item.status === 2 && item.conversationId === id);
    if (!match) { Alert.alert('Eşleşme bulunamadı', 'Bu sohbet için etkin bir eşleşme bulunmuyor.'); return; }
    setChatOptionsVisible(false);
    Alert.alert('Eşleşme sonlandırılsın mı?', 'Sohbet iki taraf için kapanır. Bu işlem geri alınamaz.', [
      { text: 'Vazgeç', style: 'cancel' },
      { text: 'Sonlandır', style: 'destructive', onPress: async () => {
        try { await matchApi.end(match.id); await qc.invalidateQueries({ queryKey: ['matches'] }); await qc.invalidateQueries({ queryKey: ['conversations'] }); router.replace('/(tabs)/chats'); }
        catch (error) { Alert.alert('Eşleşme sonlandırılamadı', error instanceof Error ? error.message : 'Tekrar dene.'); }
      } },
    ]);
  }

  if (q.isLoading) return <View style={styles.center}><Loading label="Sohbet açılıyor…" /></View>;

  return (
    <>
    <KeyboardAvoidingView style={styles.page} behavior={Platform.OS === 'ios' ? 'padding' : undefined} keyboardVerticalOffset={Platform.OS === 'ios' ? 4 : 0}>
      {/* Production Header */}
      <View style={[styles.header, { paddingTop: Math.max(insets.top, 12) }]}>
        <Pressable onPress={() => router.back()} style={styles.headerButton}>
          <ArrowLeft size={20} color={colors.ink} strokeWidth={2.2} />
        </Pressable>
        <Pressable accessibilityRole="button" accessibilityLabel="Profili görüntüle" onPress={() => userId && router.push({ pathname: '/user/[id]', params: { id: userId } })} style={styles.avatarWrap}>
          <Avatar name={profile.data?.displayName || name || 'Bağlantı'} size={42} />
          {presence.data?.isOnline ? <View style={styles.headerOnlineBadge} /> : null}
        </Pressable>
        <Pressable style={styles.identity} onPress={() => userId && router.push({ pathname: '/user/[id]', params: { id: userId } })}>
          <Text numberOfLines={1} style={styles.headerName}>{profile.data?.displayName || name || 'Bağlantı'}</Text>
          <Text style={presence.data?.isOnline ? styles.headerOnlineText : styles.headerLastSeenText}>
            {presence.data?.isOnline ? 'Çevrimiçi' : presence.data?.lastSeenAtUtc ? `Son görülme ${new Date(presence.data.lastSeenAtUtc).toLocaleString('tr-TR', { day: 'numeric', month: 'short', hour: '2-digit', minute: '2-digit' })}` : 'MindMatch bağlantısı'}
          </Text>
        </Pressable>
        <View style={styles.headerActions}>
          <Pressable accessibilityLabel="Sesli arama" onPress={() => startCall(1)} style={styles.headerActionButton}>
            <Phone size={17} color={colors.violet} strokeWidth={2.2} />
          </Pressable>
          <Pressable accessibilityLabel="Görüntülü arama" onPress={() => startCall(2)} style={styles.headerActionButton}>
            <Video size={18} color={colors.violet} strokeWidth={2.2} />
          </Pressable>
          <Pressable accessibilityLabel="Sohbet seçenekleri" onPress={openChatOptions} style={styles.headerActionButton}>
            <MoreHorizontal size={19} color={colors.ink} strokeWidth={2.2} />
          </Pressable>
        </View>
      </View>

      {/* Messages FlatList */}
      <FlatList
        ref={listRef}
        data={messages}
        keyExtractor={message => message.id}
        contentContainerStyle={styles.messagesContainer}
        keyboardShouldPersistTaps="handled"
        onContentSizeChange={() => listRef.current?.scrollToEnd({ animated: false })}
        renderItem={({ item }) => {
          const mine = item.senderId === me?.userId;
          const isBlurredPhoto = item.type === 2 && item.content?.includes('[blur]') && !revealedPhotos.has(item.id);
          const isPhoto = item.type === 2 && Boolean(item.mediaAssetId);

          return (
            <View style={[styles.bubbleRow, mine ? styles.mineRow : styles.theirRow]}>
              <Pressable delayLongPress={350} onLongPress={() => setSelectedMessage(item)} style={[styles.bubble, mine ? styles.mineBubble : styles.theirBubble]}>
                {item.isDeletedForEveryone ? (
                  <Text style={[styles.deletedText, mine && styles.deletedTextMine]}>Bu mesaj silindi</Text>
                ) : (
                  <>
                    {/* Photo Message */}
                    {isPhoto && item.mediaAssetId ? (
                      <View style={styles.photoContainer}>
                        <Pressable
                          activeOpacity={0.9}
                          onPress={() => {
                            if (isBlurredPhoto) {
                              toggleRevealPhoto(item.id, item.mediaAssetId!);
                            } else {
                              setLightboxMediaId(item.mediaAssetId);
                            }
                          }}
                        >
                          <ProtectedImage
                            mediaAssetId={item.mediaAssetId}
                            blurRadius={isBlurredPhoto ? 35 : 0}
                            style={styles.messageImage}
                          />
                          {isBlurredPhoto ? (
                            <View style={styles.blurOverlay}>
                              <View style={styles.blurBadge}>
                                <EyeOff size={18} color={colors.white} strokeWidth={2.2} />
                                <Text style={styles.blurTitle}>Gizli Fotoğraf</Text>
                                <Text style={styles.blurSubtitle}>Görüntülemek için dokun</Text>
                              </View>
                            </View>
                          ) : item.content?.includes('[blur]') ? (
                            <Pressable style={styles.reblurButton} onPress={() => toggleRevealPhoto(item.id, item.mediaAssetId!)}>
                              <Eye size={12} color={colors.white} />
                              <Text style={styles.reblurText}>Gizle</Text>
                            </Pressable>
                          ) : null}
                        </Pressable>
                      </View>
                    ) : null}

                    {/* Voice Message */}
                    {item.type === 3 && item.mediaAssetId ? (
                      <VoiceMessage mediaAssetId={item.mediaAssetId} mine={mine} />
                    ) : null}

                    {/* Text Message */}
                    {item.content && !item.content.startsWith('[blur]') ? (
                      <Text style={[styles.messageText, mine ? styles.mineText : styles.theirText]}>
                        {item.content}
                      </Text>
                    ) : null}
                  </>
                )}

                {/* Metadata & Status */}
                <View style={styles.messageMeta}>
                  <Text style={[styles.messageTime, mine ? styles.mineTime : styles.theirTime]}>
                    {new Date(item.createdAtUtc).toLocaleTimeString('tr-TR', { hour: '2-digit', minute: '2-digit' })}
                  </Text>
                  {mine && !item.isDeletedForEveryone ? (
                    item.status >= 3 ? (
                      <CheckCheck size={14} color="#D6E4FF" strokeWidth={2.4} />
                    ) : item.status === 2 ? (
                      <CheckCheck size={14} color="#FFFFFFA8" strokeWidth={2.2} />
                    ) : (
                      <Check size={13} color="#FFFFFFA8" strokeWidth={2.2} />
                    )
                  ) : null}
                </View>
              </Pressable>
            </View>
          );
        }}
        ListEmptyComponent={
          <Card style={styles.emptyCard}>
            <View style={styles.emptyIconWrap}>
              <Sparkles size={24} color={colors.violet} />
            </View>
            <Text style={styles.emptyTitle}>Sohbet burada başlıyor</Text>
            <Text style={styles.emptyText}>İlk mesajı göndererek samimi ve derin bir bağ kurabilirsin.</Text>
          </Card>
        }
      />

      {/* Upload Stage / Error Banner */}
      {uploadingImage ? (
        <View style={styles.statusBanner}>
          <ActivityIndicator size="small" color={colors.violet} />
          <Text style={styles.statusText}>{imageUploadStage}</Text>
        </View>
      ) : null}
      {imageUploadError ? (
        <View style={styles.errorBanner}>
          <Text style={styles.errorText}>{imageUploadError}</Text>
        </View>
      ) : null}

      {/* Production Composer Bar */}
      <View style={[styles.composerContainer, { paddingBottom: Math.max(insets.bottom, 12) }]}>
        {/* Fotoğraf Ekleme Butonu */}
        <Pressable
          accessibilityLabel="Fotoğraf ekle"
          onPress={choosePhotoOptions}
          disabled={recorderState.isRecording || !!recordingUri || uploadingVoice || uploadingImage}
          style={styles.composerActionButton}
        >
          {uploadingImage ? (
            <ActivityIndicator size="small" color={colors.violet} />
          ) : (
            <ImagePlus color={colors.violet} size={21} strokeWidth={2.2} />
          )}
        </Pressable>

        {/* Input veya Voice Draft */}
        {recorderState.isRecording ? (
          <View style={styles.voiceDraftWrap}>
            <View style={styles.voicePulsingDot} />
            <Text style={styles.voiceDraftText}>Ses kaydediliyor · {formatDuration(recorderState.durationMillis)}</Text>
          </View>
        ) : recordingUri ? (
          <View style={styles.voiceDraftWrap}>
            <Mic color={colors.violet} size={17} />
            <Text style={styles.voiceDraftText}>Sesli mesaj hazır · {formatDuration(voiceDurationMs)}</Text>
          </View>
        ) : (
          <TextInput
            value={text}
            onChangeText={setText}
            placeholder="Mesaj yaz…"
            placeholderTextColor="#A5A1B8"
            multiline
            maxLength={2000}
            style={styles.textInput}
          />
        )}

        {/* Aksiyon Butonları */}
        {recorderState.isRecording ? (
          <View style={styles.composerButtonsRow}>
            <Pressable accessibilityLabel="Kaydı iptal et" onPress={cancelVoiceRecording} style={styles.voiceCancelButton}>
              <X color={colors.muted} size={19} />
            </Pressable>
            <Pressable accessibilityLabel="Kaydı bitir" onPress={stopVoiceRecording} style={styles.voiceStopButton}>
              <Square color={colors.white} size={15} fill={colors.white} />
            </Pressable>
          </View>
        ) : recordingUri ? (
          <View style={styles.composerButtonsRow}>
            <Pressable accessibilityLabel="Kaydı sil" onPress={cancelVoiceRecording} disabled={uploadingVoice} style={styles.voiceCancelButton}>
              <Trash2 color={colors.red} size={18} />
            </Pressable>
            <Pressable accessibilityLabel="Sesli mesajı gönder" onPress={sendVoiceMessage} disabled={uploadingVoice} style={styles.sendButton}>
              {uploadingVoice ? <ActivityIndicator color={colors.white} size="small" /> : <Send color={colors.white} size={17} />}
            </Pressable>
          </View>
        ) : (
          <View style={styles.composerButtonsRow}>
            <Pressable accessibilityLabel="Sesli mesaj kaydet" onPress={startVoiceRecording} disabled={uploadingImage} style={styles.micButton}>
              <Mic color={colors.violet} size={20} strokeWidth={2.2} />
            </Pressable>
            <Pressable
              accessibilityLabel="Mesaj gönder"
              disabled={!text.trim() || send.isPending || uploadingImage}
              onPress={() => send.mutate()}
              style={[styles.sendButton, (!text.trim() || send.isPending || uploadingImage) && styles.sendButtonDisabled]}
            >
              <Send color={colors.white} size={17} strokeWidth={2.4} />
            </Pressable>
          </View>
        )}
      </View>
    </KeyboardAvoidingView>

    {/* Fullscreen Photo Lightbox Modal */}
    <Modal visible={Boolean(lightboxMediaId)} transparent animationType="fade" onRequestClose={() => setLightboxMediaId(null)}>
      <View style={styles.lightboxBackdrop}>
        <View style={[styles.lightboxHeader, { paddingTop: Math.max(insets.top, 14) }]}>
          <Text style={styles.lightboxTitle}>Fotoğraf</Text>
          <Pressable onPress={() => setLightboxMediaId(null)} style={styles.lightboxClose}>
            <X size={22} color={colors.white} />
          </Pressable>
        </View>
        <View style={styles.lightboxBody}>
          {lightboxMediaId ? (
            <ProtectedImage mediaAssetId={lightboxMediaId} contentFit="contain" style={styles.lightboxImage} />
          ) : null}
        </View>
      </View>
    </Modal>

    {/* Options ActionSheet Modal */}
    <Modal
      transparent
      visible={Boolean(selectedMessage || chatOptionsVisible || blockConfirmVisible || deleteAllConfirmVisible)}
      animationType="fade"
      onRequestClose={() => { setSelectedMessage(null); setChatOptionsVisible(false); setBlockConfirmVisible(false); setDeleteAllConfirmVisible(false); }}
    >
      <View style={styles.actionSheetBackdrop}>
        <Pressable style={StyleSheet.absoluteFill} onPress={() => { setSelectedMessage(null); setChatOptionsVisible(false); setBlockConfirmVisible(false); setDeleteAllConfirmVisible(false); }} />
        <View style={[styles.actionSheetCard, { paddingBottom: Math.max(insets.bottom, 24) }]}>
          {deleteAllConfirmVisible ? (
            <>
              <Text style={styles.sheetTitle}>Herkesten silinsin mi?</Text>
              <Text style={styles.sheetDesc}>Bu mesaj her iki tarafın sohbetinden kalıcı olarak kaldırılır.</Text>
              <Pressable accessibilityRole="button" onPress={() => deleteSelectedMessage(true)} style={styles.sheetActionItem}>
                <Trash2 size={18} color={colors.red} />
                <Text style={styles.sheetActionTextDanger}>Herkesten sil</Text>
              </Pressable>
              <Pressable accessibilityRole="button" onPress={() => setDeleteAllConfirmVisible(false)} style={styles.sheetCancelItem}>
                <Text style={styles.sheetCancelText}>Vazgeç</Text>
              </Pressable>
            </>
          ) : selectedMessage ? (
            <>
              <Text style={styles.sheetTitle}>Mesaj İşlemleri</Text>
              <Pressable accessibilityRole="button" onPress={() => deleteSelectedMessage(false)} style={styles.sheetActionItem}>
                <Trash2 size={18} color={colors.ink} />
                <Text style={styles.sheetActionText}>Benden sil</Text>
              </Pressable>
              {selectedMessage.senderId === me?.userId && !selectedMessage.isDeletedForEveryone ? (
                <Pressable accessibilityRole="button" onPress={() => setDeleteAllConfirmVisible(true)} style={styles.sheetActionItem}>
                  <X size={18} color={colors.red} />
                  <Text style={styles.sheetActionTextDanger}>Herkesten sil</Text>
                </Pressable>
              ) : null}
              <Pressable accessibilityRole="button" onPress={() => setSelectedMessage(null)} style={styles.sheetCancelItem}>
                <Text style={styles.sheetCancelText}>Vazgeç</Text>
              </Pressable>
            </>
          ) : blockConfirmVisible ? (
            <>
              <Text style={styles.sheetTitle}>Kişiyi engelle</Text>
              <Text style={styles.sheetDesc}>Engellediğinde bu kullanıcı sana ulaşamaz ve profilini göremez.</Text>
              <Pressable accessibilityRole="button" onPress={confirmBlock} style={styles.sheetActionItem}>
                <Shield size={18} color={colors.red} />
                <Text style={styles.sheetActionTextDanger}>Kullanıcıyı engelle</Text>
              </Pressable>
              <Pressable accessibilityRole="button" onPress={() => setBlockConfirmVisible(false)} style={styles.sheetCancelItem}>
                <Text style={styles.sheetCancelText}>Vazgeç</Text>
              </Pressable>
            </>
          ) : (
            <>
              <Text style={styles.sheetTitle}>Sohbet Seçenekleri</Text>
              <Pressable accessibilityRole="button" onPress={() => { setChatOptionsVisible(false); if (userId) router.push({ pathname: '/user/[id]', params: { id: userId } }); }} style={styles.sheetActionItem}>
                <MessageCircle size={18} color={colors.violet} />
                <Text style={styles.sheetActionText}>Profili Görüntüle</Text>
              </Pressable>
              <Pressable accessibilityRole="button" onPress={hideConversation} style={styles.sheetActionItem}>
                <Trash2 size={18} color={colors.ink} />
                <Text style={styles.sheetActionText}>Sohbeti Gizle</Text>
              </Pressable>
              <Pressable accessibilityRole="button" onPress={endMatch} style={styles.sheetActionItem}>
                <X size={18} color={colors.red} />
                <Text style={styles.sheetActionTextDanger}>Eşleşmeyi Sonlandır</Text>
              </Pressable>
              <Pressable accessibilityRole="button" onPress={() => { setChatOptionsVisible(false); router.push({ pathname: '/report', params: { userId, conversationId: id } }); }} style={styles.sheetActionItem}>
                <Flag size={18} color={colors.coral} />
                <Text style={styles.sheetActionText}>Kişiyi Bildir</Text>
              </Pressable>
              <Pressable accessibilityRole="button" onPress={() => { setChatOptionsVisible(false); setBlockConfirmVisible(true); }} style={styles.sheetActionItem}>
                <Shield size={18} color={colors.red} />
                <Text style={styles.sheetActionTextDanger}>Kişiyi Engelle</Text>
              </Pressable>
              <Pressable accessibilityRole="button" onPress={() => setChatOptionsVisible(false)} style={styles.sheetCancelItem}>
                <Text style={styles.sheetCancelText}>Kapat</Text>
              </Pressable>
            </>
          )}
        </View>
      </View>
    </Modal>
    </>
  );
}

function VoiceMessage({ mediaAssetId, mine }: { mediaAssetId: string; mine: boolean }) {
  const player = useAudioPlayer(null);
  const status = useAudioPlayerStatus(player);
  const loaded = useRef(false);
  const objectUrl = useRef<string | null>(null);
  const [loading, setLoading] = useState(false);
  const [failed, setFailed] = useState(false);

  useEffect(() => () => {
    if (activeVoicePlayer === player) activeVoicePlayer = null;
    player.pause();
    if (objectUrl.current) URL.revokeObjectURL(objectUrl.current);
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
        if (Platform.OS === 'web') {
          const response = await fetch(`${API_URL}/api/media/${mediaAssetId}`, { headers: { Authorization: `Bearer ${token}` } });
          if (!response.ok) throw new Error('Ses dosyası yüklenemedi.');
          objectUrl.current = URL.createObjectURL(await response.blob());
          player.replace(objectUrl.current);
        } else {
          player.replace({ uri: `${API_URL}/api/media/${mediaAssetId}`, headers: { Authorization: `Bearer ${token}` } });
        }
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

  return (
    <View style={styles.voiceMessageContainer}>
      <Pressable
        accessibilityLabel={status.playing ? 'Durdur' : 'Dinle'}
        onPress={togglePlayback}
        style={[styles.voicePlayBtn, mine ? styles.voicePlayBtnMine : styles.voicePlayBtnTheir]}
      >
        {loading ? (
          <ActivityIndicator size="small" color={mine ? colors.violet : colors.white} />
        ) : status.playing ? (
          <Pause color={mine ? colors.violet : colors.white} size={16} fill={mine ? colors.violet : colors.white} />
        ) : (
          <Play color={mine ? colors.violet : colors.white} size={16} fill={mine ? colors.violet : colors.white} />
        )}
      </Pressable>
      <View style={styles.voiceProgressWrap}>
        <View style={[styles.voiceTrack, mine && styles.voiceTrackMine]}>
          <View style={[styles.voiceFill, { width: `${duration ? Math.min(100, (position / duration) * 100) : 0}%` }]} />
        </View>
        <Text style={[styles.voiceTimeLabel, mine && styles.voiceTimeLabelMine]}>{label}</Text>
      </View>
    </View>
  );
}

function formatDuration(durationMs: number) {
  const seconds = Math.floor(Math.max(0, durationMs) / 1000);
  return `${Math.floor(seconds / 60)}:${String(seconds % 60).padStart(2, '0')}`;
}

const styles = StyleSheet.create({
  page: { flex: 1, backgroundColor: '#F8F7FC' },
  center: { flex: 1, backgroundColor: '#F8F7FC', alignItems: 'center', justifyContent: 'center' },

  // Header
  header: {
    backgroundColor: colors.white,
    borderBottomColor: '#EBE7F3',
    borderBottomWidth: 1,
    flexDirection: 'row',
    alignItems: 'center',
    paddingHorizontal: 14,
    paddingBottom: 11,
    gap: 9,
    shadowColor: '#171329',
    shadowOffset: { width: 0, height: 2 },
    shadowOpacity: 0.03,
    shadowRadius: 8,
    elevation: 2,
  },
  headerButton: {
    width: 38,
    height: 38,
    borderRadius: 13,
    backgroundColor: '#F3F0FA',
    alignItems: 'center',
    justifyContent: 'center',
  },
  avatarWrap: {
    position: 'relative',
  },
  headerOnlineBadge: {
    position: 'absolute',
    bottom: -1,
    right: -1,
    width: 12,
    height: 12,
    borderRadius: 6,
    backgroundColor: colors.green,
    borderWidth: 2,
    borderColor: colors.white,
  },
  identity: {
    flex: 1,
    minWidth: 0,
  },
  headerName: {
    fontSize: 14,
    fontWeight: '900',
    color: colors.ink,
    letterSpacing: -0.3,
  },
  headerOnlineText: {
    fontSize: 10,
    color: colors.green,
    fontWeight: '800',
    marginTop: 2,
  },
  headerLastSeenText: {
    fontSize: 10,
    color: colors.muted,
    fontWeight: '600',
    marginTop: 2,
  },
  headerActions: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 6,
  },
  headerActionButton: {
    width: 36,
    height: 36,
    borderRadius: 12,
    backgroundColor: '#F3F0FA',
    alignItems: 'center',
    justifyContent: 'center',
  },

  // Messages Container
  messagesContainer: {
    paddingHorizontal: 14,
    paddingVertical: 18,
    gap: 10,
    flexGrow: 1,
    justifyContent: 'flex-end',
  },
  bubbleRow: {
    width: '100%',
    flexDirection: 'row',
  },
  mineRow: {
    justifyContent: 'flex-end',
  },
  theirRow: {
    justifyContent: 'flex-start',
  },
  bubble: {
    maxWidth: '82%',
    paddingHorizontal: 14,
    paddingVertical: 10,
    borderRadius: 20,
    shadowColor: '#221543',
    shadowOffset: { width: 0, height: 2 },
    shadowOpacity: 0.04,
    shadowRadius: 6,
    elevation: 1,
  },
  mineBubble: {
    backgroundColor: colors.violet,
    borderBottomRightRadius: 4,
  },
  theirBubble: {
    backgroundColor: colors.white,
    borderColor: '#ECE8F4',
    borderWidth: 1,
    borderBottomLeftRadius: 4,
  },
  messageText: {
    fontSize: 14,
    lineHeight: 20,
  },
  mineText: {
    color: colors.white,
    fontWeight: '500',
  },
  theirText: {
    color: colors.ink,
    fontWeight: '500',
  },
  deletedText: {
    fontSize: 12,
    fontStyle: 'italic',
    color: colors.muted,
  },
  deletedTextMine: {
    color: '#FFFFFFA8',
  },

  // Photo
  photoContainer: {
    borderRadius: 16,
    overflow: 'hidden',
    position: 'relative',
    marginBottom: 4,
  },
  messageImage: {
    width: 230,
    height: 250,
    borderRadius: 16,
    backgroundColor: '#E7E0F8',
  },
  blurOverlay: {
    ...StyleSheet.absoluteFillObject,
    backgroundColor: '#17132960',
    alignItems: 'center',
    justifyContent: 'center',
    padding: 16,
  },
  blurBadge: {
    backgroundColor: '#1E1933CC',
    paddingHorizontal: 16,
    paddingVertical: 14,
    borderRadius: 18,
    alignItems: 'center',
    gap: 4,
    borderWidth: 1,
    borderColor: '#FFFFFF25',
  },
  blurTitle: {
    color: colors.white,
    fontSize: 13,
    fontWeight: '900',
    marginTop: 4,
  },
  blurSubtitle: {
    color: '#D8D4E8',
    fontSize: 10,
    fontWeight: '600',
  },
  reblurButton: {
    position: 'absolute',
    top: 8,
    right: 8,
    flexDirection: 'row',
    alignItems: 'center',
    gap: 4,
    backgroundColor: '#171329AA',
    paddingHorizontal: 8,
    paddingVertical: 4,
    borderRadius: 10,
  },
  reblurText: {
    color: colors.white,
    fontSize: 9,
    fontWeight: '800',
  },

  // Message Metadata
  messageMeta: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'flex-end',
    gap: 4,
    marginTop: 4,
  },
  messageTime: {
    fontSize: 9,
    fontWeight: '600',
  },
  mineTime: {
    color: '#FFFFFFB5',
  },
  theirTime: {
    color: '#9E9AAE',
  },

  // Voice Player
  voiceMessageContainer: {
    width: 215,
    flexDirection: 'row',
    alignItems: 'center',
    gap: 10,
    paddingVertical: 3,
  },
  voicePlayBtn: {
    width: 36,
    height: 36,
    borderRadius: 18,
    alignItems: 'center',
    justifyContent: 'center',
  },
  voicePlayBtnMine: {
    backgroundColor: colors.white,
  },
  voicePlayBtnTheir: {
    backgroundColor: colors.violet,
  },
  voiceProgressWrap: {
    flex: 1,
    gap: 5,
  },
  voiceTrack: {
    height: 5,
    borderRadius: 3,
    backgroundColor: '#E6E1F4',
    overflow: 'hidden',
  },
  voiceTrackMine: {
    backgroundColor: '#FFFFFF45',
  },
  voiceFill: {
    height: 5,
    borderRadius: 3,
    backgroundColor: colors.violet,
  },
  voiceTimeLabel: {
    fontSize: 9,
    fontWeight: '700',
    color: colors.muted,
  },
  voiceTimeLabelMine: {
    color: '#FFFFFFAA',
  },

  // Empty Card
  emptyCard: {
    alignSelf: 'center',
    marginVertical: 40,
    maxWidth: 290,
    alignItems: 'center',
    padding: 24,
    borderRadius: 22,
  },
  emptyIconWrap: {
    width: 48,
    height: 48,
    borderRadius: 16,
    backgroundColor: colors.paleViolet,
    alignItems: 'center',
    justifyContent: 'center',
    marginBottom: 12,
  },
  emptyTitle: {
    fontSize: 15,
    fontWeight: '900',
    color: colors.ink,
    textAlign: 'center',
  },
  emptyText: {
    fontSize: 11,
    color: colors.muted,
    lineHeight: 17,
    textAlign: 'center',
    marginTop: 6,
  },

  // Banners
  statusBanner: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 8,
    paddingHorizontal: 16,
    paddingVertical: 8,
    backgroundColor: colors.paleViolet,
    borderTopWidth: 1,
    borderTopColor: colors.line,
  },
  statusText: {
    color: colors.violet,
    fontSize: 11,
    fontWeight: '700',
  },
  errorBanner: {
    paddingHorizontal: 16,
    paddingVertical: 8,
    backgroundColor: colors.paleCoral,
  },
  errorText: {
    color: colors.red,
    fontSize: 11,
    fontWeight: '700',
  },

  // Composer
  composerContainer: {
    flexDirection: 'row',
    alignItems: 'flex-end',
    gap: 8,
    paddingHorizontal: 12,
    paddingTop: 10,
    backgroundColor: colors.white,
    borderTopWidth: 1,
    borderTopColor: '#EBE7F3',
    shadowColor: '#171329',
    shadowOffset: { width: 0, height: -2 },
    shadowOpacity: 0.04,
    shadowRadius: 8,
    elevation: 3,
  },
  composerActionButton: {
    width: 42,
    height: 42,
    borderRadius: 14,
    backgroundColor: '#F3F0FA',
    alignItems: 'center',
    justifyContent: 'center',
  },
  textInput: {
    flex: 1,
    minHeight: 42,
    maxHeight: 115,
    paddingHorizontal: 15,
    paddingTop: 10,
    paddingBottom: 10,
    backgroundColor: '#F6F4FB',
    borderRadius: 20,
    borderWidth: 1,
    borderColor: '#ECE8F5',
    fontSize: 14,
    color: colors.ink,
  },
  voiceDraftWrap: {
    flex: 1,
    minHeight: 42,
    flexDirection: 'row',
    alignItems: 'center',
    gap: 9,
    paddingHorizontal: 14,
    borderRadius: 20,
    backgroundColor: colors.paleViolet,
  },
  voicePulsingDot: {
    width: 10,
    height: 10,
    borderRadius: 5,
    backgroundColor: colors.coral,
  },
  voiceDraftText: {
    flex: 1,
    fontSize: 12,
    color: colors.ink,
    fontWeight: '700',
  },
  composerButtonsRow: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 6,
  },
  micButton: {
    width: 40,
    height: 40,
    borderRadius: 14,
    backgroundColor: '#F3F0FA',
    alignItems: 'center',
    justifyContent: 'center',
  },
  sendButton: {
    width: 42,
    height: 42,
    borderRadius: 14,
    backgroundColor: colors.violet,
    alignItems: 'center',
    justifyContent: 'center',
    shadowColor: colors.violet,
    shadowOffset: { width: 0, height: 2 },
    shadowOpacity: 0.3,
    shadowRadius: 4,
    elevation: 3,
  },
  sendButtonDisabled: {
    opacity: 0.45,
    shadowOpacity: 0,
  },
  voiceCancelButton: {
    width: 36,
    height: 40,
    alignItems: 'center',
    justifyContent: 'center',
  },
  voiceStopButton: {
    width: 42,
    height: 42,
    borderRadius: 14,
    backgroundColor: colors.coral,
    alignItems: 'center',
    justifyContent: 'center',
  },

  // Lightbox Modal
  lightboxBackdrop: {
    flex: 1,
    backgroundColor: '#090812F8',
    justifyContent: 'center',
  },
  lightboxHeader: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    paddingHorizontal: 20,
    paddingBottom: 15,
  },
  lightboxTitle: {
    color: colors.white,
    fontSize: 16,
    fontWeight: '900',
  },
  lightboxClose: {
    width: 38,
    height: 38,
    borderRadius: 19,
    backgroundColor: '#FFFFFF22',
    alignItems: 'center',
    justifyContent: 'center',
  },
  lightboxBody: {
    flex: 1,
    alignItems: 'center',
    justifyContent: 'center',
    padding: 10,
  },
  lightboxImage: {
    width: '100%',
    height: '100%',
  },

  // ActionSheet
  actionSheetBackdrop: {
    flex: 1,
    justifyContent: 'flex-end',
    backgroundColor: '#17132970',
  },
  actionSheetCard: {
    padding: 22,
    backgroundColor: colors.white,
    borderTopLeftRadius: 28,
    borderTopRightRadius: 28,
    shadowColor: '#171329',
    shadowOffset: { width: 0, height: -4 },
    shadowOpacity: 0.1,
    shadowRadius: 16,
    elevation: 10,
  },
  sheetTitle: {
    fontSize: 16,
    fontWeight: '900',
    color: colors.ink,
    marginBottom: 6,
  },
  sheetDesc: {
    fontSize: 12,
    color: colors.muted,
    lineHeight: 18,
    marginBottom: 12,
  },
  sheetActionItem: {
    minHeight: 52,
    flexDirection: 'row',
    alignItems: 'center',
    gap: 12,
    borderBottomWidth: 1,
    borderBottomColor: '#F0ECF6',
  },
  sheetActionText: {
    fontSize: 13,
    color: colors.ink,
    fontWeight: '700',
  },
  sheetActionTextDanger: {
    fontSize: 13,
    color: colors.red,
    fontWeight: '800',
  },
  sheetCancelItem: {
    minHeight: 48,
    alignItems: 'center',
    justifyContent: 'center',
    marginTop: 8,
  },
  sheetCancelText: {
    fontSize: 13,
    color: colors.muted,
    fontWeight: '800',
  },
});
