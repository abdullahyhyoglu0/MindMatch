using MindMatch.Application.Dtos.Media;
using MindMatch.Domain.Entities;

namespace MindMatch.Application.Interfaces;

public interface IMediaService
{
    Task<MediaAssetResponse> UploadAsync(Guid ownerId, MediaCategory category, Stream content, string suppliedContentType, CancellationToken cancellationToken = default);
    Task<(Stream Content, string ContentType)?> OpenAsync(Guid viewerId, Guid mediaAssetId, CancellationToken cancellationToken = default);
    Task<(Stream Content, string ContentType)?> OpenReportedEvidenceAsync(Guid moderatorId, Guid reportId,
        Guid mediaAssetId, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(Guid ownerId, Guid mediaAssetId, CancellationToken cancellationToken = default);
}
