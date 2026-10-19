using MindMatch.Domain.Common;

namespace MindMatch.Domain.Entities;

public enum RealtimeDestinationType { User = 1, Conversation = 2 }

public sealed class RealtimeOutboxMessage : BaseEntity
{
    public RealtimeDestinationType DestinationType { get; private set; }
    public long Sequence { get; private set; }
    public Guid DestinationId { get; private set; }
    public string EventName { get; private set; } = string.Empty;
    public string PayloadJson { get; private set; } = string.Empty;
    public DateTime ExpiresAtUtc { get; private set; }
    public DateTime? ProcessedAtUtc { get; private set; }
    public DateTime NextAttemptAtUtc { get; private set; }
    public int AttemptCount { get; private set; }
    public string? LastError { get; private set; }

    private RealtimeOutboxMessage() { }

    public RealtimeOutboxMessage(RealtimeDestinationType destinationType, Guid destinationId,
        string eventName, string payloadJson, DateTime expiresAtUtc)
    {
        if (!Enum.IsDefined(destinationType) || destinationId == Guid.Empty ||
            string.IsNullOrWhiteSpace(eventName) || eventName.Length > 80 ||
            string.IsNullOrWhiteSpace(payloadJson) || payloadJson.Length > 64_000)
            throw new ArgumentException("Gerçek zamanlı outbox olayı geçersiz.");
        if (expiresAtUtc <= DateTime.UtcNow) throw new ArgumentException("Outbox olayı için geçerlilik süresi geçmiş olamaz.");
        DestinationType = destinationType;
        DestinationId = destinationId;
        EventName = eventName.Trim();
        PayloadJson = payloadJson;
        ExpiresAtUtc = DateTime.SpecifyKind(expiresAtUtc, DateTimeKind.Utc);
        NextAttemptAtUtc = DateTime.UtcNow;
    }

    public bool MarkProcessed(DateTime nowUtc)
    {
        if (ProcessedAtUtc.HasValue) return false;
        ProcessedAtUtc = DateTime.SpecifyKind(nowUtc, DateTimeKind.Utc);
        PayloadJson = "{}";
        LastError = null;
        MarkAsUpdated();
        return true;
    }

    public bool RecordFailure(DateTime nowUtc, string error)
    {
        if (ProcessedAtUtc.HasValue || ExpiresAtUtc <= nowUtc) return false;
        AttemptCount++;
        var delaySeconds = Math.Min(300, 1 << Math.Min(AttemptCount, 8));
        NextAttemptAtUtc = DateTime.SpecifyKind(nowUtc.AddSeconds(delaySeconds), DateTimeKind.Utc);
        LastError = string.IsNullOrWhiteSpace(error) ? "Publish failed." : error[..Math.Min(error.Length, 500)];
        MarkAsUpdated();
        return true;
    }
}
