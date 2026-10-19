
using MindMatch.Application.Dtos.Message;
using MindMatch.Application.Interfaces;
using MindMatch.Domain.Entities;

namespace MindMatch.Application.Services;

public class MessageService : IMessageService
{
    private readonly IMessageRepository _messageRepository;
    private readonly IConversationRepository _conversationRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IUserBlockRepository _userBlockRepository;
    private readonly IMessageRequestRepository _messageRequestRepository;
    private readonly IMediaAssetRepository _mediaAssets;
    private readonly IUserRepository _users;
    private readonly IUserProfileRepository _profiles;
    private readonly IRealtimeOutboxRepository _outbox;
    private readonly INotificationService _notifications;

    public MessageService(
        IMessageRepository messageRepository,
        IConversationRepository conversationRepository,
        IUserBlockRepository userBlockRepository,
        IMessageRequestRepository messageRequestRepository,
        IMediaAssetRepository mediaAssets,
        IUserRepository users,
        IUserProfileRepository profiles,
        IRealtimeOutboxRepository outbox,
        INotificationService notifications,
        IUnitOfWork unitOfWork)
    {
        _messageRepository = messageRepository;
        _conversationRepository = conversationRepository;
        _userBlockRepository = userBlockRepository;
        _messageRequestRepository = messageRequestRepository;
        _mediaAssets = mediaAssets;
        _users = users;
        _profiles = profiles;
        _outbox = outbox;
        _notifications = notifications;
        _unitOfWork = unitOfWork;
    }

    public async Task<MessageResponse> SendAsync(
        Guid senderId,
        SendMessageRequest request,
        CancellationToken cancellationToken = default)
    {
        if (senderId == Guid.Empty)
            throw new ArgumentException("Kullanıcı ID boş olamaz.");
        var sender = await _users.GetByIdAsync(senderId, cancellationToken)
            ?? throw new UnauthorizedAccessException("Kullanıcı etkin değil.");
        if (!sender.IsActive) throw new UnauthorizedAccessException("Kullanıcı etkin değil.");

        if (request.ConversationId == Guid.Empty)
            throw new ArgumentException("Sohbet ID boş olamaz.");

        var conversation = await _conversationRepository.GetByIdAsync(
            request.ConversationId,
            cancellationToken);

        if (conversation is null)
            throw new KeyNotFoundException("Sohbet bulunamadı.");

        EnsureParticipant(conversation, senderId);

        var recipientId = conversation.User1Id == senderId
            ? conversation.User2Id
            : conversation.User1Id;
        var recipient = await _users.GetByIdAsync(recipientId, cancellationToken);
        if (recipient is null || !recipient.IsActive)
            throw new UnauthorizedAccessException("Alıcı hesabı etkin değil.");
        if (await _userBlockRepository.IsBlockedEitherWayAsync(
                senderId, recipientId, cancellationToken))
            throw new UnauthorizedAccessException("Engelleme nedeniyle mesaj gönderilemez.");

        var messageRequest = await _messageRequestRepository.GetByConversationIdAsync(
            conversation.Id, cancellationToken);
        if (messageRequest is not null && messageRequest.Status != MessageRequestStatus.Accepted)
            throw new InvalidOperationException("Mesaj isteği kabul edilmeden yeni mesaj gönderilemez.");

        if (!conversation.IsActive)
            throw new InvalidOperationException("Bu sohbet aktif değil.");

        if (!Enum.IsDefined(typeof(MessageType), request.Type))
            throw new ArgumentException("Geçersiz mesaj türü.");

        if (request.Type == MessageType.File)
            throw new ArgumentException("Dosya mesajları henüz desteklenmiyor.");

        if (request.Type == MessageType.Text &&
            string.IsNullOrWhiteSpace(request.Content))
            throw new ArgumentException("Metin mesajı boş olamaz.");

        if (!string.IsNullOrWhiteSpace(request.MediaUrl))
            throw new ArgumentException("Harici medya URL'leri kabul edilmiyor; yüklenen medya kimliğini kullanın.");

        var expectedCategory = request.Type switch
        {
            MessageType.Image => MediaCategory.MessageImage,
            MessageType.Voice => MediaCategory.MessageVoice,
            MessageType.Video => MediaCategory.MessageVideo,
            _ => (MediaCategory?)null
        };
        if (expectedCategory.HasValue && !request.MediaAssetId.HasValue)
            throw new ArgumentException("Bu mesaj türü için yüklenmiş medya kimliği gereklidir.");
        if (request.Type == MessageType.Text && request.MediaAssetId.HasValue)
            throw new ArgumentException("Metin mesajı medya eki alamaz.");
        if (request.MediaAssetId.HasValue)
        {
            var asset = await _mediaAssets.GetByIdAsync(request.MediaAssetId.Value, cancellationToken);
            if (asset is null || asset.OwnerId != senderId || asset.IsDeleted || asset.Category != expectedCategory)
                throw new UnauthorizedAccessException("Medya bu mesajda kullanılamaz.");
            asset.BindToConversation(conversation.Id);
            if (await _mediaAssets.IsUsedInMessageAsync(asset.Id, cancellationToken))
                throw new InvalidOperationException("Bu medya daha önce bir mesaja bağlanmış.");
        }

        if (request.Content?.Length > 5000)
            throw new ArgumentException("Mesaj 5000 karakteri aşamaz.");

        if (request.MediaUrl?.Length > 2048)
            throw new ArgumentException("Medya adresi çok uzun.");

        var message = new Message(
            request.ConversationId,
            senderId,
            request.Type,
            request.Content,
            null,
            request.MediaAssetId);
        message.SetExpiration(message.CreatedAtUtc.AddDays(conversation.MessageRetentionDays));

        await _messageRepository.AddAsync(message, cancellationToken);

        conversation.UpdateLastMessageTime(message.CreatedAtUtc);

        var response = Map(message);
        response.RecipientId = recipientId;
        await _outbox.AddAsync(RealtimeEventOutbox.ToUser(senderId, "MessageCreated", response,
            message.ExpiresAtUtc), cancellationToken);
        await _outbox.AddAsync(RealtimeEventOutbox.ToUser(recipientId, "MessageCreated", response,
            message.ExpiresAtUtc), cancellationToken);
        await _notifications.QueueAsync(recipientId, NotificationType.NewMessage,
            "Yeni mesaj", "Yeni bir mesajınız var.", conversation.Id, cancellationToken);
        var conversationUpdate = new { conversationId = conversation.Id, lastMessageAtUtc = message.CreatedAtUtc, messageId = message.Id };
        await _outbox.AddAsync(RealtimeEventOutbox.ToUser(senderId, "ConversationUpdated", conversationUpdate,
            message.ExpiresAtUtc), cancellationToken);
        await _outbox.AddAsync(RealtimeEventOutbox.ToUser(recipientId, "ConversationUpdated", conversationUpdate,
            message.ExpiresAtUtc), cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return response;
    }

    public async Task<MessagePageResponse> GetHistoryPageAsync(
        Guid conversationId,
        Guid userId,
        DateTime? beforeCreatedAtUtc,
        Guid? beforeMessageId,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        if (conversationId == Guid.Empty || userId == Guid.Empty)
            throw new ArgumentException("Kullanıcı ve sohbet kimlikleri gereklidir.");
        if ((beforeCreatedAtUtc.HasValue != beforeMessageId.HasValue) ||
            pageSize is < 1 or > 100)
            throw new ArgumentException("Sayfalama imleci veya pageSize geçersiz.");

        var conversation = await _conversationRepository.GetByIdAsync(
            conversationId, cancellationToken);
        if (conversation is null)
            throw new KeyNotFoundException("Sohbet bulunamadı.");
        EnsureParticipant(conversation, userId);
        await EnsureMessageRequestAcceptedAsync(conversationId, cancellationToken);

        var fetched = await _messageRepository.GetVisiblePageAsync(
            conversationId, userId, beforeCreatedAtUtc, beforeMessageId,
            pageSize + 1, cancellationToken);
        var hasMore = fetched.Count > pageSize;
        if (hasMore)
            fetched.RemoveAt(fetched.Count - 1);

        var ordered = fetched.OrderBy(x => x.CreatedAtUtc)
            .ThenBy(x => x.Id).ToList();

        var otherUserId = conversation.User1Id == userId ? conversation.User2Id : conversation.User1Id;
        var otherUserProfile = await _profiles.GetByUserIdAsync(otherUserId, cancellationToken);

        return new MessagePageResponse
        {
            Items = ordered.Select(message =>
            {
                var mapped = Map(message);
                MessagePrivacyMapper.ApplyReadReceiptPreference(mapped, userId,
                    otherUserProfile?.ShowReadReceipts != false);
                return mapped;
            }).ToList(),
            HasMore = hasMore,
            NextBeforeCreatedAtUtc = hasMore ? fetched[^1].CreatedAtUtc : null,
            NextBeforeMessageId = hasMore ? fetched[^1].Id : null
        };
    }

    public async Task DeleteForMeAsync(
        Guid messageId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var message = await GetAccessibleMessageAsync(messageId, userId, cancellationToken);
        if (await _messageRepository.HasDeletionAsync(messageId, userId, cancellationToken))
            return;

        await _messageRepository.AddDeletionAsync(
            new MessageDeletion(message.Id, userId), cancellationToken);
        await _outbox.AddAsync(RealtimeEventOutbox.ToUser(userId, "MessageHidden",
            new { messageId, conversationId = message.ConversationId }), cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task<MessageResponse> DeleteForEveryoneAsync(
        Guid messageId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var message = await GetAccessibleMessageAsync(messageId, userId, cancellationToken);
        var conversation = await _conversationRepository.GetByIdAsync(message.ConversationId, cancellationToken)
            ?? throw new KeyNotFoundException("Sohbet bulunamadı.");
        var wasDeleted = message.IsDeletedForEveryone;
        if (message.MediaAssetId.HasValue)
        {
            var asset = await _mediaAssets.GetByIdAsync(message.MediaAssetId.Value, cancellationToken);
            asset?.MarkDeleted();
        }
        message.DeleteForEveryone(userId);
        if (!wasDeleted)
        {
            var payload = new { messageId = message.Id, conversationId = message.ConversationId, deletedAtUtc = message.DeletedAtUtc };
            await _outbox.AddAsync(RealtimeEventOutbox.ToUser(conversation.User1Id, "MessageDeleted", payload), cancellationToken);
            await _outbox.AddAsync(RealtimeEventOutbox.ToUser(conversation.User2Id, "MessageDeleted", payload), cancellationToken);
        }
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Map(message);
    }

    public async Task MarkDeliveredAsync(Guid messageId, Guid userId, CancellationToken cancellationToken = default)
    {
        var message = await GetAccessibleMessageAsync(messageId, userId, cancellationToken);
        if (message.SenderId == userId)
            throw new UnauthorizedAccessException("Gönderen teslim bilgisini değiştiremez.");
        var changed = message.Status == MessageStatus.Sent;
        message.MarkAsDelivered();
        if (changed)
            await _outbox.AddAsync(RealtimeEventOutbox.ToUser(message.SenderId, "MessageStatusChanged",
                new { messageId = message.Id, conversationId = message.ConversationId, status = message.Status }), cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task<MessageResponse> MarkReadAsync(
        Guid messageId, Guid userId, CancellationToken cancellationToken = default)
    {
        var message = await GetAccessibleMessageAsync(messageId, userId, cancellationToken);
        if (message.SenderId == userId)
            throw new UnauthorizedAccessException("Gönderen okundu bilgisini değiştiremez.");
        var changed = message.Status != MessageStatus.Read;
        message.MarkAsRead();
        var profile = await _profiles.GetByUserIdAsync(userId, cancellationToken);
        var response = Map(message);
        if (changed && profile?.ShowReadReceipts != false)
            await _outbox.AddAsync(RealtimeEventOutbox.ToUser(message.SenderId, "MessageStatusChanged",
                new { messageId = message.Id, conversationId = message.ConversationId, status = message.Status, message.ReadAtUtc }), cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return response;
    }

    private async Task<Message> GetAccessibleMessageAsync(
        Guid messageId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        if (messageId == Guid.Empty || userId == Guid.Empty)
            throw new ArgumentException("Mesaj ve kullanıcı kimlikleri gereklidir.");

        var message = await _messageRepository.GetByIdAsync(messageId, cancellationToken)
            ?? throw new KeyNotFoundException("Mesaj bulunamadı.");
        var conversation = await _conversationRepository.GetByIdAsync(
            message.ConversationId, cancellationToken)
            ?? throw new KeyNotFoundException("Sohbet bulunamadı.");
        EnsureParticipant(conversation, userId);
        await EnsureMessageRequestAcceptedAsync(conversation.Id, cancellationToken);
        return message;
    }

    private async Task EnsureMessageRequestAcceptedAsync(Guid conversationId, CancellationToken cancellationToken)
    {
        var request = await _messageRequestRepository.GetByConversationIdAsync(conversationId, cancellationToken);
        if (request is not null && request.Status != MessageRequestStatus.Accepted)
            throw new UnauthorizedAccessException("Mesaj isteği kabul edilmeden sohbet geçmişine erişilemez.");
    }

    private static void EnsureParticipant(
        Conversation conversation,
        Guid userId)
    {
        if (conversation.User1Id != userId &&
            conversation.User2Id != userId)
        {
            throw new UnauthorizedAccessException(
                "Bu sohbete erişim yetkiniz yok.");
        }
    }

    private static MessageResponse Map(Message message)
    {
        return new MessageResponse
        {
            Id = message.Id,
            ConversationId = message.ConversationId,
            SenderId = message.SenderId,
            Content = message.Content,
            Type = message.Type,
            Status = message.Status,
            MediaUrl = message.MediaUrl,
            MediaAssetId = message.MediaAssetId,
            CreatedAtUtc = message.CreatedAtUtc,
            ReadAtUtc = message.ReadAtUtc,
            ExpiresAtUtc = message.ExpiresAtUtc,
            IsDeletedForEveryone = message.IsDeletedForEveryone,
            DeletedAtUtc = message.DeletedAtUtc
        };
    }
}
