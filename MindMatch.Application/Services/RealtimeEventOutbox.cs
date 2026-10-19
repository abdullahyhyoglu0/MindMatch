using System.Text.Json;
using MindMatch.Domain.Entities;

namespace MindMatch.Application.Services;

internal static class RealtimeEventOutbox
{
    private static readonly TimeSpan DefaultLifetime = TimeSpan.FromDays(7);
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static RealtimeOutboxMessage ToUser(Guid userId, string eventName, object payload,
        DateTime? expiresAtUtc = null) => new(RealtimeDestinationType.User, userId, eventName,
        JsonSerializer.Serialize(payload, JsonOptions), expiresAtUtc ?? DateTime.UtcNow.Add(DefaultLifetime));

    public static RealtimeOutboxMessage ToConversation(Guid conversationId, string eventName, object payload,
        DateTime? expiresAtUtc = null) => new(RealtimeDestinationType.Conversation, conversationId, eventName,
        JsonSerializer.Serialize(payload, JsonOptions), expiresAtUtc ?? DateTime.UtcNow.Add(DefaultLifetime));
}
