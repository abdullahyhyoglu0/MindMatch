namespace MindMatch.Application.Dtos.Stories;

public sealed record StoryViewerResponse(Guid ViewerId, DateTime ViewedAtUtc);
