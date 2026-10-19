using Microsoft.AspNetCore.SignalR;
using MindMatch.Application.Interfaces;

namespace MindMatch.Api.Hubs;

public sealed class SignalRMessageEventPublisher : IMessageEventPublisher
{
    private readonly IHubContext<MessagesHub> _hubContext;
    private readonly IHubContext<CallSignalingHub> _callHubContext;

    public SignalRMessageEventPublisher(IHubContext<MessagesHub> hubContext,
        IHubContext<CallSignalingHub> callHubContext)
    {
        _hubContext = hubContext;
        _callHubContext = callHubContext;
    }

    public Task PublishToUserAsync(Guid userId, string eventName, object payload, CancellationToken cancellationToken = default)
    {
        var group = MessagesHub.GetUserGroup(userId);
        var hub = eventName.StartsWith("Call", StringComparison.Ordinal)
            ? _callHubContext.Clients : _hubContext.Clients;
        return hub.Group(group).SendAsync(eventName, payload, cancellationToken);
    }

    public Task PublishToConversationAsync(Guid conversationId, string eventName, object payload,
        CancellationToken cancellationToken = default) =>
        _hubContext.Clients.Group(MessagesHub.GetConversationGroup(conversationId))
            .SendAsync(eventName, payload, cancellationToken);
}
