namespace MindMatch.Application.Interfaces;

public interface IMessageEventPublisher
{
    Task PublishToUserAsync(Guid userId, string eventName, object payload, CancellationToken cancellationToken = default);

    Task PublishToConversationAsync(Guid conversationId, string eventName, object payload,
        CancellationToken cancellationToken = default);
}
