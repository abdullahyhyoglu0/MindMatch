
using MindMatch.Application.Dtos.Conversation;
using MindMatch.Application.Interfaces;
using MindMatch.Domain.Entities;

namespace MindMatch.Application.Services;

public class ConversationService : IConversationService
{
    private readonly IConversationRepository _conversationRepository;
    private readonly IUnitOfWork _unitOfWork;

    public ConversationService(
        IConversationRepository conversationRepository,
        IUnitOfWork unitOfWork)
    {
        _conversationRepository = conversationRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<List<ConversationResponse>> GetMyConversationsAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        ValidateUserId(userId);

        var conversations =
            await _conversationRepository.GetByUserIdAsync(
                userId,
                cancellationToken);

        return conversations.Select(x => new ConversationResponse
        {
            Id = x.Id,
            OtherUserId = x.User1Id == userId
                ? x.User2Id
                : x.User1Id,
            LastMessageAtUtc = x.LastMessageAtUtc,
            CreatedAtUtc = x.CreatedAtUtc
        }).ToList();
    }

    public async Task HideConversationAsync(
        Guid conversationId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        ValidateUserId(userId);

        if (conversationId == Guid.Empty)
            throw new ArgumentException("Sohbet ID boş olamaz.");

        var conversation =
            await _conversationRepository.GetByIdAsync(
                conversationId,
                cancellationToken);

        if (conversation is null)
            throw new InvalidOperationException(
                "Sohbet bulunamadı.");

        conversation.HideForUser(userId);

        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task<ConversationResponse> GetAsync(Guid conversationId, Guid userId, CancellationToken cancellationToken = default)
    {
        ValidateUserId(userId);
        var conversation = await _conversationRepository.GetByIdAsync(conversationId, cancellationToken)
            ?? throw new KeyNotFoundException("Sohbet bulunamadı.");
        EnsureParticipant(conversation, userId);
        return Map(conversation, userId);
    }

    public async Task RestoreConversationAsync(Guid conversationId, Guid userId, CancellationToken cancellationToken = default)
    {
        ValidateUserId(userId);
        var conversation = await _conversationRepository.GetByIdAsync(conversationId, cancellationToken)
            ?? throw new KeyNotFoundException("Sohbet bulunamadı.");
        EnsureParticipant(conversation, userId);
        conversation.ShowForUser(userId);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private static ConversationResponse Map(Conversation x, Guid userId) => new()
    {
        Id = x.Id,
        OtherUserId = x.User1Id == userId ? x.User2Id : x.User1Id,
        LastMessageAtUtc = x.LastMessageAtUtc,
        CreatedAtUtc = x.CreatedAtUtc
    };

    private static void EnsureParticipant(Conversation conversation, Guid userId)
    {
        if (conversation.User1Id != userId && conversation.User2Id != userId)
            throw new UnauthorizedAccessException("Bu sohbete erişim yetkiniz yok.");
    }

    private static void ValidateUserId(Guid userId)
    {
        if (userId == Guid.Empty)
            throw new ArgumentException("Kullanıcı ID boş olamaz.");
    }
}
