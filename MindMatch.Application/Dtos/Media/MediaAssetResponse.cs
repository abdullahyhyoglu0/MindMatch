using MindMatch.Domain.Entities;

namespace MindMatch.Application.Dtos.Media;

public sealed class MediaAssetResponse
{
    public Guid Id { get; init; }
    public MediaCategory Category { get; init; }
    public string ContentType { get; init; } = string.Empty;
    public long LengthBytes { get; init; }
    public int? Width { get; init; }
    public int? Height { get; init; }
}
