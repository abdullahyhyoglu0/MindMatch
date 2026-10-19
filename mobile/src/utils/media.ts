import * as ImageManipulator from 'expo-image-manipulator';

/** Normalize iPhone HEIC/AVIF and large camera images to the JPEG format accepted by the API. */
export async function normalizeImageForUpload(uri: string) {
  const result = await ImageManipulator.manipulateAsync(
    uri,
    [{ resize: { width: 1920 } }],
    { format: ImageManipulator.SaveFormat.JPEG, compress: 0.82 },
  );
  return { uri: result.uri, fileName: 'mindmatch-image.jpg', mimeType: 'image/jpeg' };
}

export function uploadFailureMessage(error: unknown) {
  if (!(error instanceof Error)) return 'Dosya yüklenemedi. İnternet bağlantını kontrol edip tekrar dene.';
  const status = 'status' in error ? Number(error.status) : 0;
  if (status === 0) return 'Sunucuya ulaşılamadı. API bağlantısını kontrol edip tekrar dene.';
  if (status === 413) return 'Dosya çok büyük. Daha küçük bir görsel veya kısa bir video seç.';
  if (status === 429) return 'Çok sık yükleme yapıldı. Biraz bekleyip tekrar dene.';
  return error.message || 'Dosya yüklenemedi. Tekrar dene.';
}
