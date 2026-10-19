using MindMatch.Application.Dtos.Media;
using MindMatch.Application.Interfaces;
using MindMatch.Domain.Entities;

namespace MindMatch.Application.Services;

public sealed class MediaService : IMediaService
{
    private const int MaxImageBytes = 10 * 1024 * 1024;
    private const int MaxAudioBytes = 15 * 1024 * 1024;
    private const int MaxVideoBytes = 50 * 1024 * 1024;
    private readonly IMediaStorage _storage;
    private readonly IMediaAssetRepository _assets;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMessageRepository _messages;
    private readonly IConversationRepository _conversations;
    private readonly IUserBlockRepository _blocks;
    private readonly IProfilePhotoRepository _profilePhotos;
    private readonly IUserProfileRepository _profiles;
    private readonly IStoryRepository _stories;
    private readonly IUserRepository _users;
    private readonly IReportRepository _reports;

    public MediaService(IMediaStorage storage, IMediaAssetRepository assets, IUnitOfWork unitOfWork,
        IMessageRepository messages, IConversationRepository conversations, IUserBlockRepository blocks,
        IProfilePhotoRepository profilePhotos, IUserProfileRepository profiles,
        IStoryRepository stories, IUserRepository users, IReportRepository reports)
    { _storage = storage; _assets = assets; _unitOfWork = unitOfWork; _messages = messages; _conversations = conversations; _blocks = blocks; _profilePhotos = profilePhotos; _profiles = profiles; _stories = stories; _users = users; _reports = reports; }

    public async Task<MediaAssetResponse> UploadAsync(Guid ownerId, MediaCategory category, Stream content,
        string suppliedContentType, CancellationToken cancellationToken = default)
    {
        if (ownerId == Guid.Empty || content is null || !Enum.IsDefined(category))
            throw new ArgumentException("Medya yükleme bilgileri geçersiz.");
        var maxBytes = category switch
        {
            MediaCategory.ProfilePhoto or MediaCategory.MessageImage or MediaCategory.StoryImage => MaxImageBytes,
            MediaCategory.MessageVoice => MaxAudioBytes,
            MediaCategory.MessageVideo => MaxVideoBytes,
            MediaCategory.StoryVideo => MaxVideoBytes,
            _ => throw new ArgumentException("Desteklenmeyen medya kategorisi.")
        };

        using var buffer = new MemoryStream();
        var chunk = new byte[64 * 1024];
        int read;
        while ((read = await content.ReadAsync(chunk, cancellationToken)) > 0)
        {
            if (buffer.Length + read > maxBytes)
                throw new ArgumentException($"Dosya boyutu {maxBytes / (1024 * 1024)} MB sınırını aşıyor.");
            await buffer.WriteAsync(chunk.AsMemory(0, read), cancellationToken);
        }
        var bytes = buffer.ToArray();
        var detected = ValidateContent(bytes, category, suppliedContentType);
        var storageKey = await _storage.SaveAsync(category, detected.Extension, bytes, cancellationToken);
        try
        {
            var asset = new MediaAsset(ownerId, category, storageKey, detected.ContentType,
                bytes.LongLength, detected.Width, detected.Height);
            await _assets.AddAsync(asset, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return Map(asset);
        }
        catch
        {
            await _storage.DeleteAsync(storageKey, CancellationToken.None);
            throw;
        }
    }

    public async Task<(Stream Content, string ContentType)?> OpenAsync(Guid viewerId, Guid mediaAssetId,
        CancellationToken cancellationToken = default)
    {
        if (viewerId == Guid.Empty || mediaAssetId == Guid.Empty) throw new ArgumentException("Kimlik geçersiz.");
        var asset = await _assets.GetByIdAsync(mediaAssetId, cancellationToken);
        if (asset is null || asset.IsDeleted) return null;
        if (asset.Category == MediaCategory.ProfilePhoto)
        {
            var photo = await _profilePhotos.GetByMediaAssetIdAsync(mediaAssetId, cancellationToken);
            if (photo is null || photo.UserId != asset.OwnerId) return null;
            var profile = await _profiles.GetByUserIdAsync(photo.UserId, cancellationToken);
            var owner = await _users.GetByIdAsync(photo.UserId, cancellationToken);
            if (owner is null || !owner.IsActive || profile is null) return null;
            if (viewerId != photo.UserId && (!profile.IsProfileVisible ||
                await _blocks.IsBlockedEitherWayAsync(viewerId, photo.UserId, cancellationToken))) return null;
        }
        else if (asset.Category is MediaCategory.StoryImage or MediaCategory.StoryVideo)
        {
            var story = await _stories.GetByMediaAssetIdAsync(mediaAssetId, cancellationToken);
            if (story is null || story.UserId != asset.OwnerId || !story.IsAvailableAt(DateTime.UtcNow)) return null;
            var owner = await _users.GetByIdAsync(story.UserId, cancellationToken);
            if (owner is null || !owner.IsActive) return null;
            if (viewerId != story.UserId)
            {
                var profile = await _profiles.GetByUserIdAsync(story.UserId, cancellationToken);
                if (profile is null || !profile.IsProfileVisible ||
                    await _blocks.IsBlockedEitherWayAsync(viewerId, story.UserId, cancellationToken)) return null;
            }
        }
        else if (asset.Category is MediaCategory.MessageImage or MediaCategory.MessageVoice or MediaCategory.MessageVideo)
        {
            var message = await _messages.GetByMediaAssetIdAsync(mediaAssetId, cancellationToken);
            if (message is null || message.SenderId != asset.OwnerId || asset.ConversationId != message.ConversationId || message.IsDeletedForEveryone ||
                (message.ExpiresAtUtc.HasValue && message.ExpiresAtUtc.Value <= DateTime.UtcNow) ||
                await _messages.HasDeletionAsync(message.Id, viewerId, cancellationToken)) return null;
            var conversation = await _conversations.GetByIdAsync(message.ConversationId, cancellationToken);
            if (conversation is null || (conversation.User1Id != viewerId && conversation.User2Id != viewerId) ||
                await _blocks.IsBlockedEitherWayAsync(viewerId, message.SenderId, cancellationToken)) return null;
        }
        else return null;
        var stream = await _storage.OpenReadAsync(asset.StorageKey, cancellationToken);
        return stream is null ? null : (stream, asset.ContentType);
    }

    public async Task<bool> DeleteAsync(Guid ownerId, Guid mediaAssetId,
        CancellationToken cancellationToken = default)
    {
        if (ownerId == Guid.Empty || mediaAssetId == Guid.Empty)
            throw new ArgumentException("Kimlik geçersiz.");
        var asset = await _assets.GetByIdAsync(mediaAssetId, cancellationToken);
        if (asset is null || asset.OwnerId != ownerId) return false;
        if (asset.IsDeleted) return true;
        if (await _assets.IsReferencedAsync(mediaAssetId, cancellationToken))
            throw new InvalidOperationException("Kullanımdaki medya silinemez.");
        asset.MarkDeleted();
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<(Stream Content, string ContentType)?> OpenReportedEvidenceAsync(
        Guid moderatorId, Guid reportId, Guid mediaAssetId,
        CancellationToken cancellationToken = default)
    {
        if (moderatorId == Guid.Empty || reportId == Guid.Empty || mediaAssetId == Guid.Empty)
            throw new ArgumentException("Kimlik geçersiz.");
        var moderator = await _users.GetByIdAsync(moderatorId, cancellationToken);
        if (moderator is null || !moderator.IsActive ||
            moderator.Role is not (UserRole.Admin or UserRole.Moderator))
            throw new UnauthorizedAccessException("Moderatör yetkisi gereklidir.");

        var report = await _reports.GetByIdAsync(reportId, cancellationToken);
        if (report is null) return null;
        var now = DateTime.UtcNow;
        if (!report.Messages.Any(x => x.IsMediaEvidenceAvailableAt(mediaAssetId, now)))
            return null;

        var asset = await _assets.GetByIdAsync(mediaAssetId, cancellationToken);
        if (asset is null || asset.OwnerId != report.ReportedUserId ||
            asset.Category is not (MediaCategory.MessageImage or MediaCategory.MessageVoice or MediaCategory.MessageVideo))
            return null;

        var stream = await _storage.OpenReadAsync(asset.StorageKey, cancellationToken);
        if (stream is null) return null;
        try
        {
            await _reports.AddAuditAsync(new ModerationAudit(moderatorId,
                ModerationActionType.ReportEvidenceViewed, "Rapor medya kanıtı görüntülendi.",
                report.ReportedUserId, report.Id), cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            await stream.DisposeAsync();
            throw;
        }
        return (stream, asset.ContentType);
    }

    private static MediaAssetResponse Map(MediaAsset x) => new()
    {
        Id = x.Id, Category = x.Category, ContentType = x.ContentType,
        LengthBytes = x.LengthBytes, Width = x.Width, Height = x.Height
    };

    internal static (string ContentType, string Extension, int? Width, int? Height) ValidateContent(
        byte[] bytes, MediaCategory category, string suppliedContentType)
    {
        ArgumentNullException.ThrowIfNull(bytes);
        if (category is MediaCategory.ProfilePhoto or MediaCategory.MessageImage or MediaCategory.StoryImage)
        {
            if (TryGetPngDimensions(bytes, out var width, out var height))
            {
                if (width is < 1 or > 8192 || height is < 1 or > 8192 || (long)width * height > 40_000_000)
                    throw new ArgumentException("Görsel çözünürlüğü izin verilen sınırların dışında.");
                if (!IsContentType(suppliedContentType, "image/png"))
                    throw new ArgumentException("Dosya geçerli PNG görseli değil.");
                return ("image/png", "png", width, height);
            }
            if (TryGetJpegDimensions(bytes, out var jpegWidth, out var jpegHeight))
            {
                if (jpegWidth > 8192 || jpegHeight > 8192 || (long)jpegWidth * jpegHeight > 40_000_000 ||
                    !IsContentType(suppliedContentType, "image/jpeg"))
                    throw new ArgumentException("JPEG görseli boyut sınırını aşıyor veya içerik türü hatalı.");
                return ("image/jpeg", "jpg", jpegWidth, jpegHeight);
            }
            throw new ArgumentException("Yalnızca doğrulanabilir PNG ve JPEG görselleri destekleniyor.");
        }

        if (category == MediaCategory.MessageVoice)
        {
            if (IsValidWav(bytes) && IsContentType(suppliedContentType, "audio/wav"))
                return ("audio/wav", "wav", null, null);
            if (IsValidOggPage(bytes) && IsContentType(suppliedContentType, "audio/ogg"))
                return ("audio/ogg", "ogg", null, null);
            if (HasValidMp3Frame(bytes) && IsContentType(suppliedContentType, "audio/mpeg"))
                return ("audio/mpeg", "mp3", null, null);
            if (IsValidWebmHeader(bytes) && IsContentType(suppliedContentType, "audio/webm"))
                return ("audio/webm", "webm", null, null);
            if (IsValidAudioMp4(bytes) && IsContentType(suppliedContentType, "audio/mp4"))
                return ("audio/mp4", "m4a", null, null);
            throw new ArgumentException("Ses dosyası içeriği doğrulanamadı.");
        }

        if (category is MediaCategory.StoryVideo or MediaCategory.MessageVideo &&
            IsValidMp4(bytes) && IsContentType(suppliedContentType, "video/mp4"))
            return ("video/mp4", "mp4", null, null);
        throw new ArgumentException("Video dosyası içeriği doğrulanamadı.");
    }

    private static bool StartsWith(ReadOnlySpan<byte> bytes, ReadOnlySpan<byte> prefix) => bytes.StartsWith(prefix);
    private static bool IsContentType(string supplied, string expected) => string.Equals(supplied?.Split(';')[0].Trim(), expected, StringComparison.OrdinalIgnoreCase);

    private static bool TryGetPngDimensions(byte[] bytes, out int width, out int height)
    {
        width = height = 0;
        ReadOnlySpan<byte> signature = [137, 80, 78, 71, 13, 10, 26, 10];
        if (bytes.Length < 57 || !bytes.AsSpan(0, 8).SequenceEqual(signature)) return false;
        var position = 8;
        var hasHeader = false;
        var hasImageData = false;
        while (position <= bytes.Length - 12)
        {
            var chunkLengthRaw = System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(position, 4));
            if (chunkLengthRaw > int.MaxValue) return false;
            var chunkLength = (int)chunkLengthRaw;
            var chunkEndLong = (long)position + chunkLength + 12;
            if (chunkEndLong > bytes.Length) return false;
            var type = bytes.AsSpan(position + 4, 4);
            var data = bytes.AsSpan(position + 8, chunkLength);
            var storedCrc = System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(position + 8 + chunkLength, 4));
            if (CalculatePngCrc(type, data) != storedCrc) return false;

            if (!hasHeader)
            {
                if (!type.SequenceEqual("IHDR"u8) || chunkLength != 13) return false;
                width = System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(data[..4]);
                height = System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(data.Slice(4, 4));
                if (data[10] != 0 || data[11] != 0 || data[12] != 0) return false;
                hasHeader = true;
            }
            else if (type.SequenceEqual("IHDR"u8)) return false;

            if (type.SequenceEqual("IDAT"u8))
            {
                if (chunkLength == 0) return false;
                hasImageData = true;
            }
            if (type.SequenceEqual("IEND"u8))
                return chunkLength == 0 && hasImageData && chunkEndLong == bytes.Length;
            position = (int)chunkEndLong;
        }
        return false;
    }

    private static uint CalculatePngCrc(ReadOnlySpan<byte> type, ReadOnlySpan<byte> data)
    {
        uint crc = 0xFFFFFFFF;
        foreach (var value in type)
            crc = (crc >> 8) ^ PngCrcTable[(crc ^ value) & 0xFF];
        foreach (var value in data)
            crc = (crc >> 8) ^ PngCrcTable[(crc ^ value) & 0xFF];
        return ~crc;
    }

    private static readonly uint[] PngCrcTable = CreatePngCrcTable();

    private static uint[] CreatePngCrcTable()
    {
        var table = new uint[256];
        for (uint index = 0; index < table.Length; index++)
        {
            var value = index;
            for (var bit = 0; bit < 8; bit++)
                value = (value & 1) == 1 ? 0xEDB88320u ^ (value >> 1) : value >> 1;
            table[index] = value;
        }
        return table;
    }

    private static bool IsValidWav(byte[] bytes)
    {
        if (bytes.Length < 44 || !StartsWith(bytes, "RIFF"u8) || !StartsWith(bytes.AsSpan(8), "WAVE"u8)) return false;
        var riffLength = System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(4, 4));
        if ((long)riffLength + 8 != bytes.Length) return false;
        var position = 12;
        var hasFormat = false;
        var hasData = false;
        while (position <= bytes.Length - 8)
        {
            var chunkLength = System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(position + 4, 4));
            var nextLong = (long)position + 8 + chunkLength + (chunkLength & 1);
            if (nextLong > bytes.Length) return false;
            var type = bytes.AsSpan(position, 4);
            if (type.SequenceEqual("fmt "u8))
            {
                if (chunkLength < 16) return false;
                var format = System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(position + 8, 2));
                var channels = System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(position + 10, 2));
                var sampleRate = System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(position + 12, 4));
                if (format == 0 || channels == 0 || sampleRate == 0) return false;
                hasFormat = true;
            }
            else if (type.SequenceEqual("data"u8) && chunkLength > 0) hasData = true;
            position = (int)nextLong;
        }
        return position == bytes.Length && hasFormat && hasData;
    }

    private static bool IsValidOggPage(byte[] bytes)
    {
        if (bytes.Length < 28 || !StartsWith(bytes, "OggS"u8) || bytes[4] != 0) return false;
        var segmentCount = bytes[26];
        var tableEnd = 27 + segmentCount;
        if (segmentCount == 0 || bytes.Length <= tableEnd) return false;
        var bodyLength = 0;
        for (var index = 27; index < tableEnd; index++) bodyLength += bytes[index];
        return bodyLength > 0 && tableEnd + bodyLength <= bytes.Length;
    }

    private static bool HasValidMp3Frame(byte[] bytes)
    {
        if (bytes.Length < 4) return false;
        var frameOffset = 0;
        if (StartsWith(bytes, "ID3"u8))
        {
            if (bytes.Length < 10 || bytes[3] is < 2 or > 4 ||
                (bytes[6] | bytes[7] | bytes[8] | bytes[9]) >= 128) return false;
            var tagSize = (bytes[6] << 21) | (bytes[7] << 14) | (bytes[8] << 7) | bytes[9];
            frameOffset = 10 + tagSize + (bytes[3] == 4 && (bytes[5] & 0x10) != 0 ? 10 : 0);
        }
        if (frameOffset < 0 || frameOffset > bytes.Length - 4) return false;
        var header = System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(frameOffset, 4));
        if ((header & 0xFFE00000) != 0xFFE00000) return false;
        var version = (int)((header >> 19) & 0x3);
        var layer = (int)((header >> 17) & 0x3);
        var bitrateIndex = (int)((header >> 12) & 0xF);
        var sampleIndex = (int)((header >> 10) & 0x3);
        if (version == 1 || layer == 0 || bitrateIndex is 0 or 15 || sampleIndex == 3) return false;
        var bitrate = GetMp3BitrateKbps(version, layer, bitrateIndex);
        var sampleRate = new[] { 44100, 48000, 32000 }[sampleIndex] / (version == 3 ? 1 : version == 2 ? 2 : 4);
        var padding = (int)((header >> 9) & 1);
        var frameLength = layer == 3
            ? ((12 * bitrate * 1000 / sampleRate) + padding) * 4
            : ((version != 3 && layer == 1 ? 72 : 144) * bitrate * 1000 / sampleRate) + padding;
        return frameLength >= 4 && frameOffset + frameLength <= bytes.Length;
    }

    private static int GetMp3BitrateKbps(int version, int layer, int index)
    {
        int[] mpeg1Layer1 = [0, 32, 64, 96, 128, 160, 192, 224, 256, 288, 320, 352, 384, 416, 448, 0];
        int[] mpeg1Layer2 = [0, 32, 48, 56, 64, 80, 96, 112, 128, 160, 192, 224, 256, 320, 384, 0];
        int[] mpeg1Layer3 = [0, 32, 40, 48, 56, 64, 80, 96, 112, 128, 160, 192, 224, 256, 320, 0];
        int[] mpeg2Layer1 = [0, 32, 48, 56, 64, 80, 96, 112, 128, 144, 160, 176, 192, 224, 256, 0];
        int[] mpeg2Other = [0, 8, 16, 24, 32, 40, 48, 56, 64, 80, 96, 112, 128, 144, 160, 0];
        if (version == 3) return (layer switch { 3 => mpeg1Layer1, 2 => mpeg1Layer2, _ => mpeg1Layer3 })[index];
        return (layer == 3 ? mpeg2Layer1 : mpeg2Other)[index];
    }

    private static bool IsValidWebmHeader(byte[] bytes) => bytes.Length >= 32 &&
        bytes.AsSpan(0, 4).SequenceEqual(new byte[] { 0x1A, 0x45, 0xDF, 0xA3 }) &&
        bytes.AsSpan(4, Math.Min(bytes.Length - 4, 128)).IndexOf("webm"u8) >= 0;

    private static bool IsValidMp4(byte[] bytes)
    {
        if (bytes.Length < 32) return false;
        var position = 0;
        var hasFileType = false;
        var hasMediaData = false;
        var hasMovie = false;
        while (position <= bytes.Length - 8)
        {
            var boxSize32 = System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(position, 4));
            var type = bytes.AsSpan(position + 4, 4);
            ulong headerSize = 8;
            ulong boxSize;
            if (boxSize32 == 1)
            {
                if (position > bytes.Length - 16) return false;
                boxSize = System.Buffers.Binary.BinaryPrimitives.ReadUInt64BigEndian(bytes.AsSpan(position + 8, 8));
                headerSize = 16;
            }
            else boxSize = boxSize32 == 0 ? (ulong)(bytes.Length - position) : boxSize32;
            if (boxSize < headerSize || boxSize > (ulong)(bytes.Length - position)) return false;
            if (type.SequenceEqual("ftyp"u8))
            {
                if (boxSize < 16 || (boxSize - 16) % 4 != 0) return false;
                hasFileType = true;
            }
            else if (type.SequenceEqual("mdat"u8)) hasMediaData = boxSize > headerSize;
            else if (type.SequenceEqual("moov"u8) || type.SequenceEqual("moof"u8)) hasMovie = boxSize > headerSize;
            position += checked((int)boxSize);
            if (boxSize32 == 0) break;
        }
        return position == bytes.Length && hasFileType && hasMediaData && hasMovie;
    }

    private static bool IsValidAudioMp4(byte[] bytes)
    {
        if (!IsValidMp4(bytes)) return false;
        if (!TryFindMp4Box(bytes, 0, bytes.Length, "moov"u8, out var moovStart, out var moovEnd)) return false;

        for (var track = moovStart; TryFindMp4Box(bytes, track, moovEnd, "trak"u8, out var trackStart, out var trackEnd); track = trackEnd)
        {
            if (!TryFindMp4Box(bytes, trackStart, trackEnd, "mdia"u8, out var mediaStart, out var mediaEnd) ||
                !TryFindMp4Box(bytes, mediaStart, mediaEnd, "hdlr"u8, out var handlerStart, out var handlerEnd))
                continue;

            // hdlr payload: version/flags (4 bytes), predefined (4 bytes), handler type (4 bytes).
            if (handlerEnd - handlerStart >= 12 && bytes.AsSpan(handlerStart + 8, 4).SequenceEqual("soun"u8) &&
                bytes.AsSpan(trackStart, trackEnd - trackStart).IndexOf("mp4a"u8) >= 0)
                return true;
        }
        return false;
    }

    private static bool TryFindMp4Box(byte[] bytes, int start, int end, ReadOnlySpan<byte> wantedType,
        out int payloadStart, out int payloadEnd)
    {
        payloadStart = payloadEnd = 0;
        var position = start;
        while (position <= end - 8)
        {
            var size32 = System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(position, 4));
            var type = bytes.AsSpan(position + 4, 4);
            var headerSize = 8;
            ulong boxSize = size32 == 0 ? (ulong)(end - position) : size32;
            if (size32 == 1)
            {
                if (position > end - 16) return false;
                boxSize = System.Buffers.Binary.BinaryPrimitives.ReadUInt64BigEndian(bytes.AsSpan(position + 8, 8));
                headerSize = 16;
            }
            if (boxSize < (ulong)headerSize || boxSize > (ulong)(end - position)) return false;
            var boxEnd = position + checked((int)boxSize);
            if (type.SequenceEqual(wantedType))
            {
                payloadStart = position + headerSize;
                payloadEnd = boxEnd;
                return true;
            }
            if (size32 == 0) break;
            position = boxEnd;
        }
        return false;
    }

    private static bool TryGetJpegDimensions(byte[] bytes, out int width, out int height)
    {
        width = height = 0;
        if (bytes.Length < 4 || bytes[0] != 0xFF || bytes[1] != 0xD8 || bytes[^2] != 0xFF || bytes[^1] != 0xD9) return false;
        var position = 2;
        var hasFrame = false;
        var hasScan = false;
        while (position + 4 <= bytes.Length - 2)
        {
            if (bytes[position++] != 0xFF) return false;
            while (position < bytes.Length && bytes[position] == 0xFF) position++;
            if (position >= bytes.Length) return false;
            var marker = bytes[position++];
            if (marker == 0xD9) return hasFrame && hasScan && position == bytes.Length - 1;
            if (marker == 0x00 || marker is 0xD8 or 0x01 || marker is >= 0xD0 and <= 0xD7) return false;
            if (position + 2 > bytes.Length) return false;
            var segmentLength = (bytes[position] << 8) | bytes[position + 1];
            if (segmentLength < 2 || position + segmentLength > bytes.Length) return false;
            if (marker is 0xC0 or 0xC1 or 0xC2 or 0xC3 or 0xC5 or 0xC6 or 0xC7 or 0xC9 or 0xCA or 0xCB or 0xCD or 0xCE or 0xCF)
            {
                if (segmentLength < 8 || hasFrame) return false;
                height = (bytes[position + 3] << 8) | bytes[position + 4];
                width = (bytes[position + 5] << 8) | bytes[position + 6];
                if (width <= 0 || height <= 0) return false;
                var components = bytes[position + 7];
                if (components == 0 || segmentLength != 8 + 3 * components) return false;
                hasFrame = true;
            }
            if (marker == 0xDA)
            {
                if (!hasFrame || segmentLength < 6) return false;
                var components = bytes[position + 2];
                if (components == 0 || segmentLength != 6 + 2 * components) return false;
                hasScan = true;
                position += segmentLength;
                while (position < bytes.Length - 1)
                {
                    if (bytes[position++] != 0xFF) continue;
                    if (position >= bytes.Length) return false;
                    var scanMarker = bytes[position++];
                    if (scanMarker == 0x00 || scanMarker is >= 0xD0 and <= 0xD7) continue;
                    position -= 2;
                    break;
                }
                continue;
            }
            position += segmentLength;
        }
        return false;
    }
}
