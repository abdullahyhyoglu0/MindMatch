
using MindMatch.Application.Dtos.Match;
using MindMatch.Application.Interfaces;
using MindMatch.Domain.Entities;

namespace MindMatch.Application.Services;

public class MatchService : IMatchService
{
    private readonly IMatchRepository _matchRepository;
    private readonly IConversationRepository _conversationRepository;
    private readonly IUserBlockRepository _userBlockRepository;
    private readonly IMessageRequestRepository _messageRequestRepository;
    private readonly IUserRepository _users;
    private readonly IPremiumService _premiumService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IRealtimeOutboxRepository _outbox;
    private readonly INotificationService _notifications;

    public MatchService(
        IMatchRepository matchRepository,
        IConversationRepository conversationRepository,
        IUserBlockRepository userBlockRepository,
        IMessageRequestRepository messageRequestRepository,
        IUserRepository users,
        IPremiumService premiumService,
        IRealtimeOutboxRepository outbox,
        INotificationService notifications,
        IUnitOfWork unitOfWork)
    {
        _matchRepository = matchRepository;
        _conversationRepository = conversationRepository;
        _userBlockRepository = userBlockRepository;
        _messageRequestRepository = messageRequestRepository;
        _users = users;
        _premiumService = premiumService;
        _outbox = outbox;
        _notifications = notifications;
        _unitOfWork = unitOfWork;
    }

    public async Task<MatchResponse> SendRequestAsync(
        Guid requesterId,
        Guid receiverId,
        CancellationToken cancellationToken = default)
    {
        ValidateUserId(requesterId);
        ValidateUserId(receiverId);

        if (requesterId == receiverId)
        {
            throw new InvalidOperationException(
                "Kendinize eşleşme isteği gönderemezsiniz.");
        }

        var requester = await _users.GetByIdAsync(requesterId, cancellationToken);
        var receiver = await _users.GetByIdAsync(receiverId, cancellationToken);
        if (requester is null || !requester.IsActive || receiver is null || !receiver.IsActive)
            throw new InvalidOperationException("Eşleşme için iki hesabın da etkin olması gerekir.");

        if (await _userBlockRepository.IsBlockedEitherWayAsync(
                requesterId, receiverId, cancellationToken))
            throw new UnauthorizedAccessException("Engelleme nedeniyle eşleşme isteği gönderilemez.");

        var existingMatch =
            await _matchRepository.HasPendingOrAcceptedMatchAsync(
                requesterId,
                receiverId,
                cancellationToken);

        if (existingMatch)
        {
            throw new InvalidOperationException(
                "Bu kullanıcıyla bekleyen veya kabul edilmiş bir eşleşmeniz zaten var.");
        }

        var match = new Match(requesterId, receiverId);

        await _matchRepository.AddAsync(
            match, cancellationToken);

        await _outbox.AddAsync(RealtimeEventOutbox.ToUser(receiverId, "MatchCreated", MapToResponse(match)), cancellationToken);
        await _notifications.QueueAsync(receiverId, NotificationType.MatchRequestReceived,
            "Yeni eşleşme isteği", "Bir kullanıcı sizinle eşleşmek istiyor.", match.Id, cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return MapToResponse(match);
    }

    public async Task<MatchResponse> AcceptRequestAsync(
        Guid matchId,
        Guid respondingUserId,
        CancellationToken cancellationToken = default)
    {
        var match = await GetRequiredMatchAsync(
            matchId, cancellationToken);

        if (match.ReceiverId != respondingUserId)
            throw new UnauthorizedAccessException("Yalnızca isteği alan kullanıcı kabul edebilir.");
        var requester = await _users.GetByIdAsync(match.RequesterId, cancellationToken);
        var receiver = await _users.GetByIdAsync(match.ReceiverId, cancellationToken);
        if (requester is null || !requester.IsActive || receiver is null || !receiver.IsActive)
            throw new InvalidOperationException("Her iki hesap da etkin olmalıdır.");

        if (await _userBlockRepository.IsBlockedEitherWayAsync(
                match.RequesterId, match.ReceiverId, cancellationToken))
            throw new UnauthorizedAccessException("Engelleme nedeniyle eşleşme kabul edilemez.");

        match.Accept(respondingUserId);

        var conversation = await _conversationRepository.GetByUsersAsync(
            match.RequesterId, match.ReceiverId, cancellationToken);
        if (conversation is null)
        {
            var retentionDays = await _premiumService.GetConversationRetentionDaysAsync(
                match.RequesterId, match.ReceiverId, cancellationToken);
            conversation = new Conversation(match.RequesterId, match.ReceiverId, retentionDays);
            await _conversationRepository.AddAsync(conversation, cancellationToken);
        }
        else if (!conversation.IsActive)
        {
            conversation.Activate();
        }

        match.LinkConversation(conversation.Id);
        var messageRequest = await _messageRequestRepository.GetByConversationIdAsync(
            conversation.Id, cancellationToken);
        messageRequest?.MarkMatched();

        await _outbox.AddAsync(RealtimeEventOutbox.ToUser(match.RequesterId, "MatchAccepted", MapToResponse(match)), cancellationToken);
        await _notifications.QueueAsync(match.RequesterId, NotificationType.MatchAccepted,
            "Eşleşme isteği kabul edildi", "Eşleşme isteğiniz kabul edildi.", match.Id, cancellationToken);
        var conversationUpdate = new { conversationId = conversation.Id, otherUserId = match.ReceiverId };
        await _outbox.AddAsync(RealtimeEventOutbox.ToUser(match.RequesterId, "ConversationUpdated", conversationUpdate), cancellationToken);
        await _outbox.AddAsync(RealtimeEventOutbox.ToUser(match.ReceiverId, "ConversationUpdated",
            new { conversationId = conversation.Id, otherUserId = match.RequesterId }), cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return MapToResponse(match);
    }

    public async Task<MatchResponse> RejectRequestAsync(
        Guid matchId,
        Guid respondingUserId,
        CancellationToken cancellationToken = default)
    {
        var match = await GetRequiredMatchAsync(
            matchId, cancellationToken);

        match.Reject(respondingUserId);

        await _outbox.AddAsync(RealtimeEventOutbox.ToUser(match.RequesterId, "MatchRejected", MapToResponse(match)), cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return MapToResponse(match);
    }

    public async Task<MatchResponse> EndMatchAsync(
        Guid matchId,
        Guid requestingUserId,
        CancellationToken cancellationToken = default)
    {
        ValidateUserId(requestingUserId);

        var match = await GetRequiredMatchAsync(
            matchId, cancellationToken);

        match.End(requestingUserId);

        var recipientId = match.RequesterId == requestingUserId ? match.ReceiverId : match.RequesterId;
        Conversation? conversation = null;
        if (match.ConversationId.HasValue)
        {
            conversation = await _conversationRepository.GetByIdAsync(match.ConversationId.Value, cancellationToken);
            if (conversation is not null && conversation.IsActive)
                conversation.Deactivate();
        }
        await _outbox.AddAsync(RealtimeEventOutbox.ToUser(recipientId, "MatchEnded", MapToResponse(match)), cancellationToken);
        if (conversation is not null)
        {
            var update = new { conversationId = conversation.Id, isActive = conversation.IsActive };
            await _outbox.AddAsync(RealtimeEventOutbox.ToUser(conversation.User1Id, "ConversationUpdated", update), cancellationToken);
            await _outbox.AddAsync(RealtimeEventOutbox.ToUser(conversation.User2Id, "ConversationUpdated", update), cancellationToken);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return MapToResponse(match);
    }

    public async Task<List<MatchResponse>> GetMyMatchesAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        ValidateUserId(userId);

        var matches = await _matchRepository.GetByUserIdAsync(
            userId, cancellationToken);

        return matches
            .Select(MapToResponse)
            .ToList();
    }

    public async Task<List<MatchResponse>> GetIncomingRequestsAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        ValidateUserId(userId);
        var requests = await _matchRepository.GetIncomingPendingAsync(userId, cancellationToken);
        return requests.Select(MapToResponse).ToList();
    }

    private async Task<Match> GetRequiredMatchAsync(
        Guid matchId,
        CancellationToken cancellationToken)
    {
        if (matchId == Guid.Empty)
        {
            throw new ArgumentException(
                "Eşleşme kimliği geçersiz.",
                nameof(matchId));
        }

        return await _matchRepository.GetByIdAsync(
            matchId, cancellationToken)
            ?? throw new InvalidOperationException(
                "Eşleşme isteği bulunamadı.");
    }

    private static MatchResponse MapToResponse(Match match)
    {
        return new MatchResponse
        {
            Id = match.Id,
            RequesterId = match.RequesterId,
            ReceiverId = match.ReceiverId,
            Status = match.Status,
            CreatedAtUtc = match.CreatedAtUtc,
            RespondedAtUtc = match.RespondedAtUtc,
            EndedAtUtc = match.EndedAtUtc
        };
    }

    private static void ValidateUserId(Guid userId)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException(
                "Kullanıcı kimliği geçersiz.",
                nameof(userId));
        }
    }
}
