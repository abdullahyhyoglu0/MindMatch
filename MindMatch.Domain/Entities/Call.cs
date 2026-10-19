
using MindMatch.Domain.Common;

namespace MindMatch.Domain.Entities;

public class Call : BaseEntity
{
    public Guid ConversationId { get; private set; }

    public Guid CallerId { get; private set; }

    public Guid ReceiverId { get; private set; }

    public CallType Type { get; private set; }

    public CallStatus Status { get; private set; }

    public DateTime StartedAtUtc { get; private set; }

    public DateTime? AnsweredAtUtc { get; private set; }

    public DateTime? EndedAtUtc { get; private set; }
    public int Version { get; private set; }

    public Conversation Conversation { get; private set; } = null!;

    private Call() { }

    public Call(
        Guid conversationId,
        Guid callerId,
        Guid receiverId,
        CallType type)
    {
        if (conversationId == Guid.Empty ||
            callerId == Guid.Empty ||
            receiverId == Guid.Empty)
            throw new ArgumentException("ID alanları boş olamaz.");

        if (callerId == receiverId)
            throw new ArgumentException("Arayan ve alıcı aynı olamaz.");
        if (!Enum.IsDefined(type))
            throw new ArgumentException("Arama türü geçersiz.");

        ConversationId = conversationId;
        CallerId = callerId;
        ReceiverId = receiverId;
        Type = type;
        Status = CallStatus.Ringing;
        StartedAtUtc = DateTime.UtcNow;
    }

    public void Answer()
    {
        if (Status != CallStatus.Ringing)
            throw new InvalidOperationException("Arama yanıtlanamaz.");

        Status = CallStatus.Answered;
        AnsweredAtUtc = DateTime.UtcNow;
        Version++;
        MarkAsUpdated();
    }

    public void Reject()
    {
        if (Status != CallStatus.Ringing)
            throw new InvalidOperationException("Arama reddedilemez.");

        Status = CallStatus.Rejected;
        EndedAtUtc = DateTime.UtcNow;
        Version++;
        MarkAsUpdated();
    }

    public void End()
    {
        if (Status != CallStatus.Answered &&
            Status != CallStatus.Ringing)
            throw new InvalidOperationException("Arama zaten bitmiş.");

        Status = CallStatus.Ended;
        EndedAtUtc = DateTime.UtcNow;
        Version++;
        MarkAsUpdated();
    }

    public void MarkAsMissed()
    {
        if (Status != CallStatus.Ringing)
            throw new InvalidOperationException("Arama kaçırılmış olarak işaretlenemez.");

        Status = CallStatus.Missed;
        EndedAtUtc = DateTime.UtcNow;
        Version++;
        MarkAsUpdated();
    }
}

public enum CallType
{
    Audio = 1,
    Video = 2
}

public enum CallStatus
{
    Ringing = 1,
    Answered = 2,
    Rejected = 3,
    Missed = 4,
    Ended = 5
}
