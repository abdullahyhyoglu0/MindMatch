using MindMatch.Application.Dtos.Message;
using MindMatch.Application.Interfaces;
using MindMatch.Domain.Entities;

namespace MindMatch.Application.Services;

public sealed class MessageRequestService : IMessageRequestService
{
    private readonly IMessageRequestRepository _requests;
    private readonly IConversationRepository _conversations;
    private readonly IMessageRepository _messages;
    private readonly IUserProfileRepository _profiles;
    private readonly IUserRepository _users;
    private readonly IUserBlockRepository _blocks;
    private readonly IUserFollowRepository _follows;
    private readonly IMatchRepository _matches;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPremiumService _premiumService;
    private readonly IRealtimeOutboxRepository _outbox;
    private readonly INotificationService _notifications;

    public MessageRequestService(IMessageRequestRepository requests,
        IConversationRepository conversations, IMessageRepository messages,
        IUserProfileRepository profiles, IUserRepository users,
        IUserBlockRepository blocks, IUserFollowRepository follows, IMatchRepository matches,
        IPremiumService premiumService, IRealtimeOutboxRepository outbox,
        INotificationService notifications, IUnitOfWork unitOfWork)
    {
        _requests = requests;
        _conversations = conversations;
        _messages = messages;
        _profiles = profiles;
        _users = users;
        _blocks = blocks;
        _follows = follows;
        _matches = matches;
        _premiumService = premiumService;
        _outbox = outbox;
        _notifications = notifications;
        _unitOfWork = unitOfWork;
    }

    public async Task<MessageResponse> CreateAsync(Guid senderId, CreateMessageRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (senderId == Guid.Empty || request.RecipientId == Guid.Empty || string.IsNullOrWhiteSpace(request.Content))
            throw new ArgumentException("Gönderen, alıcı ve ilk mesaj gereklidir.");
        if (request.Content.Length > 5000) throw new ArgumentException("Mesaj 5000 karakteri aşamaz.");
        if (senderId == request.RecipientId) throw new ArgumentException("Kendinize mesaj isteği gönderemezsiniz.");

        var sender = await _users.GetByIdAsync(senderId, cancellationToken)
            ?? throw new UnauthorizedAccessException("Hesap etkin değil.");
        if (!sender.IsActive) throw new UnauthorizedAccessException("Hesap etkin değil.");

        var recipient = await _users.GetByIdAsync(request.RecipientId, cancellationToken)
            ?? throw new KeyNotFoundException("Alıcı bulunamadı.");
        if (!recipient.IsActive) throw new InvalidOperationException("Alıcı hesabı etkin değil.");
        var profile = await _profiles.GetByUserIdAsync(request.RecipientId, cancellationToken)
            ?? throw new KeyNotFoundException("Alıcı profili bulunamadı.");
        if (await _blocks.IsBlockedEitherWayAsync(senderId, request.RecipientId, cancellationToken))
            throw new UnauthorizedAccessException("Engelleme nedeniyle mesaj isteği gönderilemez.");
        if (await _conversations.GetByUsersAsync(senderId, request.RecipientId, cancellationToken) is not null)
            throw new InvalidOperationException("Bu kullanıcıyla sohbetiniz zaten mevcut.");

        var canView = await SocialAccess.CanViewProfileAsync(senderId, profile, _follows, cancellationToken);
        var isAcceptedFollower = await _follows.IsAcceptedAsync(senderId, request.RecipientId, cancellationToken);
        var isAcceptedMatch = await _matches.HasAcceptedMatchAsync(senderId, request.RecipientId, cancellationToken);
        var mode = SocialAccess.ResolveMessaging(profile, canView || isAcceptedMatch, false);
        if (mode == DiscoverMessagingMode.Unavailable && !isAcceptedFollower && !isAcceptedMatch)
            throw new InvalidOperationException("Bu kullanıcıya keşfetten mesaj gönderilemez.");

        var useRequest = mode == DiscoverMessagingMode.Request && !isAcceptedFollower && !isAcceptedMatch;
        var retentionDays = await _premiumService.GetConversationRetentionDaysAsync(
            senderId, request.RecipientId, cancellationToken);
        var conversation = new Conversation(senderId, request.RecipientId, retentionDays);
        var message = new Message(conversation.Id, senderId, MessageType.Text, request.Content.Trim());
        message.SetExpiration(message.CreatedAtUtc.AddDays(conversation.MessageRetentionDays));
        conversation.UpdateLastMessageTime(message.CreatedAtUtc);

        await _conversations.AddAsync(conversation, cancellationToken);
        if (useRequest)
            await _requests.AddAsync(new MessageRequest(conversation.Id, senderId, request.RecipientId), cancellationToken);
        await _messages.AddAsync(message, cancellationToken);
        if (useRequest)
        {
            await _outbox.AddAsync(RealtimeEventOutbox.ToUser(request.RecipientId, "MessageRequestReceived",
                new { message.ConversationId, SenderId = senderId }, message.ExpiresAtUtc), cancellationToken);
            await _notifications.QueueAsync(request.RecipientId, NotificationType.MessageRequestReceived,
                "Yeni mesaj isteği", "Bir kullanıcı size mesaj isteği gönderdi.", conversation.Id, cancellationToken);
        }
        else
        {
            var responsePreview = new { message.ConversationId, SenderId = senderId, message.Id };
            await _outbox.AddAsync(RealtimeEventOutbox.ToUser(senderId, "MessageCreated", responsePreview,
                message.ExpiresAtUtc), cancellationToken);
            await _outbox.AddAsync(RealtimeEventOutbox.ToUser(request.RecipientId, "MessageCreated", responsePreview,
                message.ExpiresAtUtc), cancellationToken);
            await _notifications.QueueAsync(request.RecipientId, NotificationType.NewMessage,
                "Yeni mesaj", "Yeni bir mesajınız var.", conversation.Id, cancellationToken);
            var conversationUpdate = new { conversationId = conversation.Id, lastMessageAtUtc = message.CreatedAtUtc, messageId = message.Id };
            await _outbox.AddAsync(RealtimeEventOutbox.ToUser(senderId, "ConversationUpdated", conversationUpdate,
                message.ExpiresAtUtc), cancellationToken);
            await _outbox.AddAsync(RealtimeEventOutbox.ToUser(request.RecipientId, "ConversationUpdated", conversationUpdate,
                message.ExpiresAtUtc), cancellationToken);
        }
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new MessageResponse
        {
            Id = message.Id, ConversationId = message.ConversationId, SenderId = message.SenderId,
            RecipientId = request.RecipientId,
            Content = message.Content, Type = message.Type, Status = message.Status,
            CreatedAtUtc = message.CreatedAtUtc,
            ExpiresAtUtc = message.ExpiresAtUtc,
            IsPendingRequest = useRequest
        };
    }

    public async Task<List<MessageRequestResponse>> GetIncomingAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty) throw new ArgumentException("Kullanıcı kimliği geçersiz.");
        var requests = await _requests.GetIncomingAsync(userId, cancellationToken);
        var result = new List<MessageRequestResponse>(requests.Count);
        foreach (var request in requests)
        {
            var message = await _messages.GetFirstBySenderAsync(request.ConversationId, request.SenderId, cancellationToken);
            result.Add(new MessageRequestResponse
            {
                ConversationId = request.ConversationId, SenderId = request.SenderId,
                ReceiverId = request.ReceiverId, CreatedAtUtc = request.CreatedAtUtc,
                InitialMessage = message?.Content
            });
        }
        return result;
    }

    public async Task AcceptAsync(Guid conversationId, Guid userId, CancellationToken cancellationToken = default)
    {
        var request = await GetRequest(conversationId, cancellationToken);
        if (request.ReceiverId != userId)
            throw new UnauthorizedAccessException("Yalnızca mesaj isteğini alan kullanıcı kabul edebilir.");
        var sender = await _users.GetByIdAsync(request.SenderId, cancellationToken);
        var receiver = await _users.GetByIdAsync(request.ReceiverId, cancellationToken);
        if (sender is null || !sender.IsActive || receiver is null || !receiver.IsActive)
            throw new InvalidOperationException("Her iki hesap da etkin olmalıdır.");
        if (await _blocks.IsBlockedEitherWayAsync(request.SenderId, request.ReceiverId, cancellationToken))
            throw new InvalidOperationException("Engelleme nedeniyle mesaj isteği kabul edilemez.");
        request.Accept(userId);
        await _outbox.AddAsync(RealtimeEventOutbox.ToUser(request.SenderId, "MessageRequestAccepted",
            new { request.ConversationId, request.ReceiverId }), cancellationToken);
        await _notifications.QueueAsync(request.SenderId, NotificationType.MessageRequestAccepted,
            "Mesaj isteği kabul edildi", "Mesaj isteğiniz kabul edildi.", conversationId, cancellationToken);
        await _outbox.AddAsync(RealtimeEventOutbox.ToUser(request.SenderId, "ConversationUpdated",
            new { conversationId = request.ConversationId, otherUserId = request.ReceiverId }), cancellationToken);
        await _outbox.AddAsync(RealtimeEventOutbox.ToUser(request.ReceiverId, "ConversationUpdated",
            new { conversationId = request.ConversationId, otherUserId = request.SenderId }), cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task RejectAsync(Guid conversationId, Guid userId, CancellationToken cancellationToken = default)
    {
        var request = await GetRequest(conversationId, cancellationToken);
        request.Reject(userId);
        await _outbox.AddAsync(RealtimeEventOutbox.ToUser(request.SenderId, "MessageRequestRejected",
            new { request.ConversationId, request.ReceiverId }), cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private async Task<MessageRequest> GetRequest(Guid conversationId, CancellationToken cancellationToken)
    {
        if (conversationId == Guid.Empty) throw new ArgumentException("Sohbet kimliği geçersiz.");
        return await _requests.GetByConversationIdAsync(conversationId, cancellationToken)
            ?? throw new KeyNotFoundException("Mesaj isteği bulunamadı.");
    }
}
