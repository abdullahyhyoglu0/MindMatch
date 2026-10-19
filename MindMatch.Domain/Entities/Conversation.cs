
using MindMatch.Domain.Common;

namespace MindMatch.Domain.Entities;

public class Conversation : BaseEntity
{
    public Guid User1Id { get; private set; }

    public Guid User2Id { get; private set; }

    public DateTime? LastMessageAtUtc { get; private set; }

    public bool IsActive { get; private set; } = true;
    public int MessageRetentionDays { get; private set; } = 1;
    public bool User1HasHiddenConversation { get; private set; }
    public bool User2HasHiddenConversation { get; private set; }

    public ICollection<Message> Messages { get; private set; }
        = new List<Message>();
    public ICollection<Call> Calls { get; private set; } = new List<Call>();
    public ICollection<Match> Matches { get; private set; }
    = new List<Match>();

    private Conversation()
    {
    }

    public Conversation(Guid user1Id, Guid user2Id, int messageRetentionDays = 1)
    {
        if (user1Id == Guid.Empty)
            throw new ArgumentException("Birinci kullanıcı ID boş olamaz.");

        if (user2Id == Guid.Empty)
            throw new ArgumentException("İkinci kullanıcı ID boş olamaz.");

        if (user1Id == user2Id)
            throw new ArgumentException("Kullanıcılar aynı olamaz.");
        if (messageRetentionDays is not (1 or 7))
            throw new ArgumentOutOfRangeException(nameof(messageRetentionDays));
        MessageRetentionDays = messageRetentionDays;

        if (user1Id.CompareTo(user2Id) < 0)
        {
            User1Id = user1Id;
            User2Id = user2Id;
        }
        else
        {
            User1Id = user2Id;
            User2Id = user1Id;
        }
    }

    public void SetMessageRetentionDays(int days)
    {
        if (LastMessageAtUtc.HasValue) throw new InvalidOperationException("Mesaj saklama süresi sohbet başladıktan sonra değiştirilemez.");
        if (days is not (1 or 7)) throw new ArgumentOutOfRangeException(nameof(days));
        MessageRetentionDays = days;
        MarkAsUpdated();
    }
    public void HideForUser(Guid userId)
    {
        if (userId == User1Id)
            User1HasHiddenConversation = true;
        else if (userId == User2Id)
            User2HasHiddenConversation = true;
        else
            throw new InvalidOperationException(
                "Bu sohbet size ait değil.");

        MarkAsUpdated();
    }

    public void ShowForUser(Guid userId)
    {
        if (userId == User1Id)
            User1HasHiddenConversation = false;
        else if (userId == User2Id)
            User2HasHiddenConversation = false;
        else
            throw new InvalidOperationException(
                "Bu sohbet size ait değil.");

        MarkAsUpdated();
    }
    public void UpdateLastMessageTime(DateTime messageTimeUtc)
    {
        LastMessageAtUtc = messageTimeUtc;
        MarkAsUpdated();
    }

    public void Deactivate()
    {
        IsActive = false;
        MarkAsUpdated();
    }

    public void Activate()
    {
        IsActive = true;
        MarkAsUpdated();
    }
}
