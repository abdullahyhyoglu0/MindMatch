using MindMatch.Application.Dtos.Calls;
using MindMatch.Application.Interfaces;
using MindMatch.Domain.Entities;

namespace MindMatch.Application.Services;

public sealed class CallService : ICallService
{
    private readonly ICallRepository _calls;
    private readonly IConversationRepository _conversations;
    private readonly IMatchRepository _matches;
    private readonly IMessageRequestRepository _messageRequests;
    private readonly IUserProfileRepository _profiles;
    private readonly IUserBlockRepository _blocks;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IUserRepository _users;
    private readonly IRealtimeOutboxRepository _outbox;
    private readonly INotificationService _notifications;

    public CallService(ICallRepository calls, IConversationRepository conversations,
        IMatchRepository matches, IUserProfileRepository profiles,
        IMessageRequestRepository messageRequests, IUserBlockRepository blocks, IUserRepository users, IRealtimeOutboxRepository outbox,
        INotificationService notifications, IUnitOfWork unitOfWork)
    {
        _calls = calls;
        _conversations = conversations;
        _matches = matches;
        _messageRequests = messageRequests;
        _profiles = profiles;
        _blocks = blocks;
        _users = users;
        _outbox = outbox;
        _notifications = notifications;
        _unitOfWork = unitOfWork;
    }

    public async Task<CallResponse> StartAsync(Guid callerId, Guid conversationId, CallType type, CancellationToken cancellationToken = default)
    {
        if (callerId == Guid.Empty || conversationId == Guid.Empty || !Enum.IsDefined(type))
            throw new ArgumentException("Arama bilgileri geçersiz.");
        var caller = await _users.GetByIdAsync(callerId, cancellationToken);
        if (caller is null || !caller.IsActive) throw new UnauthorizedAccessException("Hesap etkin değil.");
        var conversation = await _conversations.GetByIdAsync(conversationId, cancellationToken)
            ?? throw new KeyNotFoundException("Sohbet bulunamadı.");
        var receiverId = conversation.User1Id == callerId ? conversation.User2Id
            : conversation.User2Id == callerId ? conversation.User1Id : Guid.Empty;
        if (receiverId == Guid.Empty) throw new UnauthorizedAccessException("Bu sohbetin katılımcısı değilsiniz.");
        var receiver = await _users.GetByIdAsync(receiverId, cancellationToken);
        if (receiver is null || !receiver.IsActive) throw new InvalidOperationException("Alıcı hesabı etkin değil.");
        if (!conversation.IsActive) throw new InvalidOperationException("Sohbet etkin değil.");
        if (await _blocks.IsBlockedEitherWayAsync(callerId, receiverId, cancellationToken))
            throw new UnauthorizedAccessException("Engelleme nedeniyle arama başlatılamaz.");

        var messageRequest = await _messageRequests.GetByConversationIdAsync(conversationId, cancellationToken);
        if (messageRequest is not null && messageRequest.Status != MessageRequestStatus.Accepted)
            throw new UnauthorizedAccessException("Mesaj isteği kabul edilmeden arama başlatılamaz.");

        var isMatched = await _matches.HasAcceptedMatchAsync(callerId, receiverId, cancellationToken);
        if (!isMatched)
        {
            var receiverProfile = await _profiles.GetByUserIdAsync(receiverId, cancellationToken);
            if (receiverProfile is null || !receiverProfile.AllowCallsFromNonMatches)
                throw new UnauthorizedAccessException("Bu kullanıcı eşleşme dışı arama kabul etmiyor.");
        }

        var call = new Call(conversationId, callerId, receiverId, type);
        await _calls.AddAsync(call, cancellationToken);
        await _outbox.AddAsync(RealtimeEventOutbox.ToUser(receiverId, "CallIncoming", Map(call),
            call.StartedAtUtc.AddDays(1)), cancellationToken);
        await _notifications.QueueAsync(receiverId, NotificationType.IncomingCall,
            "Gelen arama", "Bir kullanıcı sizi arıyor.", call.Id, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Map(call);
    }

    public Task<CallResponse> AnswerAsync(Guid callId, Guid userId, CancellationToken cancellationToken = default) =>
        ChangeAsync(callId, userId, (call, id) =>
        {
            if (call.ReceiverId != id) throw new UnauthorizedAccessException("Yalnızca aranan kişi aramayı yanıtlayabilir.");
            call.Answer();
        }, cancellationToken, enforceCallAccess: true);

    public Task<CallResponse> RejectAsync(Guid callId, Guid userId, CancellationToken cancellationToken = default) =>
        ChangeAsync(callId, userId, (call, id) =>
        {
            if (call.ReceiverId != id) throw new UnauthorizedAccessException("Yalnızca aranan kişi aramayı reddedebilir.");
            call.Reject();
        }, cancellationToken, enforceCallAccess: true);

    public Task<CallResponse> EndAsync(Guid callId, Guid userId, CancellationToken cancellationToken = default) =>
        ChangeAsync(callId, userId, (call, id) =>
        {
            if (call.CallerId != id && call.ReceiverId != id)
                throw new UnauthorizedAccessException("Bu aramanın katılımcısı değilsiniz.");
            call.End();
        }, cancellationToken, enforceCallAccess: false);

    public async Task<CallResponse> GetForSignalingAsync(Guid callId, Guid userId, CancellationToken cancellationToken = default)
    {
        var call = await GetCall(callId, cancellationToken);
        if (call.CallerId != userId && call.ReceiverId != userId)
            throw new UnauthorizedAccessException("Bu aramanın katılımcısı değilsiniz.");
        await EnsureCallAccessAsync(call, userId, cancellationToken);
        if (call.Status is not (CallStatus.Ringing or CallStatus.Answered))
            throw new InvalidOperationException("Arama sinyalleşme için etkin değil.");
        return Map(call);
    }

    public async Task<CallResponse> GetStatusAsync(Guid callId, Guid userId, CancellationToken cancellationToken = default)
    {
        if (callId == Guid.Empty || userId == Guid.Empty)
            throw new ArgumentException("Arama ve kullanıcı kimliği gereklidir.");
        var call = await GetCall(callId, cancellationToken);
        if (call.CallerId != userId && call.ReceiverId != userId)
            throw new UnauthorizedAccessException("Bu aramanın katılımcısı değilsiniz.");
        return Map(call);
    }

    public async Task<List<CallResponse>> GetByConversationAsync(Guid conversationId, Guid userId, CancellationToken cancellationToken = default)
    {
        var conversation = await _conversations.GetByIdAsync(conversationId, cancellationToken)
            ?? throw new KeyNotFoundException("Sohbet bulunamadı.");
        if (conversation.User1Id != userId && conversation.User2Id != userId)
            throw new UnauthorizedAccessException("Bu sohbetin katılımcısı değilsiniz.");
        return (await _calls.GetByConversationIdAsync(conversationId, cancellationToken)).Select(Map).ToList();
    }

    private async Task<CallResponse> ChangeAsync(Guid callId, Guid userId, Action<Call, Guid> change,
        CancellationToken cancellationToken, bool enforceCallAccess)
    {
        var call = await GetCall(callId, cancellationToken);
        if (enforceCallAccess) await EnsureCallAccessAsync(call, userId, cancellationToken);
        change(call, userId);
        var recipientId = call.CallerId == userId ? call.ReceiverId : call.CallerId;
        var eventName = call.Status switch
        {
            CallStatus.Answered => "CallAccepted",
            CallStatus.Rejected => "CallRejected",
            CallStatus.Ended => "CallEnded",
            _ => null
        };
        if (eventName is not null)
            await _outbox.AddAsync(RealtimeEventOutbox.ToUser(recipientId, eventName, Map(call),
                call.StartedAtUtc.AddDays(1)), cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Map(call);
    }

    private async Task<Call> GetCall(Guid callId, CancellationToken cancellationToken) =>
        await _calls.GetByIdAsync(callId, cancellationToken) ?? throw new KeyNotFoundException("Arama bulunamadı.");

    private async Task EnsureCallAccessAsync(Call call, Guid userId, CancellationToken cancellationToken)
    {
        if (call.CallerId != userId && call.ReceiverId != userId)
            throw new UnauthorizedAccessException("Bu aramanın katılımcısı değilsiniz.");
        if (await _blocks.IsBlockedEitherWayAsync(call.CallerId, call.ReceiverId, cancellationToken))
            throw new UnauthorizedAccessException("Engelleme nedeniyle arama işlemi yapılamaz.");
        var caller = await _users.GetByIdAsync(call.CallerId, cancellationToken);
        var receiver = await _users.GetByIdAsync(call.ReceiverId, cancellationToken);
        if (caller is null || !caller.IsActive || receiver is null || !receiver.IsActive)
            throw new UnauthorizedAccessException("Arama katılımcılarından birinin hesabı etkin değil.");
        var request = await _messageRequests.GetByConversationIdAsync(call.ConversationId, cancellationToken);
        if (request is not null && request.Status != MessageRequestStatus.Accepted)
            throw new UnauthorizedAccessException("Mesaj isteği kabul edilmeden arama yapılamaz.");
    }

    private static CallResponse Map(Call call) => new()
    {
        Id = call.Id, ConversationId = call.ConversationId, CallerId = call.CallerId,
        ReceiverId = call.ReceiverId, Type = call.Type, Status = call.Status,
        StartedAtUtc = call.StartedAtUtc, AnsweredAtUtc = call.AnsweredAtUtc,
        EndedAtUtc = call.EndedAtUtc
    };
}
