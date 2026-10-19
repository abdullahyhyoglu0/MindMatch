
using MindMatch.Domain.Common;

namespace MindMatch.Domain.Entities;

public class StoryView : BaseEntity
{
    public Guid StoryId { get; private set; }

    public Guid ViewerId { get; private set; }

    public DateTime ViewedAtUtc { get; private set; }

    public Story Story { get; private set; } = null!;

    private StoryView() { }

    public StoryView(Guid storyId, Guid viewerId)
    {
        if (storyId == Guid.Empty || viewerId == Guid.Empty)
            throw new ArgumentException("ID alanları boş olamaz.");

        StoryId = storyId;
        ViewerId = viewerId;
        ViewedAtUtc = DateTime.UtcNow;
    }
}
