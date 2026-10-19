using MindMatch.Domain.Entities;

namespace MindMatch.Application.Interfaces;

public interface IMediaStorage
{
    Task<string> SaveAsync(MediaCategory category, string extension, ReadOnlyMemory<byte> content, CancellationToken cancellationToken = default);
    Task<Stream?> OpenReadAsync(string storageKey, CancellationToken cancellationToken = default);
    Task DeleteAsync(string storageKey, CancellationToken cancellationToken = default);
}
