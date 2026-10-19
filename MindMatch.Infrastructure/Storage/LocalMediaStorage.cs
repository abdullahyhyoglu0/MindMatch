using Microsoft.Extensions.Options;
using MindMatch.Application.Interfaces;
using MindMatch.Domain.Entities;

namespace MindMatch.Infrastructure.Storage;

public sealed class LocalMediaStorage : IMediaStorage
{
    private readonly string _root;

    public LocalMediaStorage(IOptions<MediaStorageSettings> options)
    {
        _root = Path.IsPathRooted(options.Value.RootPath)
            ? Path.GetFullPath(options.Value.RootPath)
            : Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, options.Value.RootPath));
    }

    public async Task<string> SaveAsync(MediaCategory category, string extension,
        ReadOnlyMemory<byte> content, CancellationToken cancellationToken = default)
    {
        if (!Enum.IsDefined(category) || extension is not ("jpg" or "png" or "mp3" or "wav" or "ogg" or "webm" or "mp4"))
            throw new ArgumentException("Desteklenmeyen medya türü.");
        var key = $"{category.ToString().ToLowerInvariant()}/{Guid.NewGuid():N}.{extension}";
        var path = ResolvePath(key);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None,
            64 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan);
        await stream.WriteAsync(content, cancellationToken);
        return key;
    }

    public Task<Stream?> OpenReadAsync(string storageKey, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var path = ResolvePath(storageKey);
        Stream? result = File.Exists(path)
            ? new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 64 * 1024,
                FileOptions.Asynchronous | FileOptions.SequentialScan)
            : null;
        return Task.FromResult(result);
    }

    public Task DeleteAsync(string storageKey, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var path = ResolvePath(storageKey);
        if (File.Exists(path)) File.Delete(path);
        return Task.CompletedTask;
    }

    private string ResolvePath(string key)
    {
        var segments = key.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length != 2 || !Enum.TryParse<MediaCategory>(segments[0], true, out _) ||
            !Guid.TryParseExact(Path.GetFileNameWithoutExtension(segments[1]), "N", out _) ||
            Path.GetExtension(segments[1]) is not (".jpg" or ".png" or ".mp3" or ".wav" or ".ogg" or ".webm" or ".mp4"))
            throw new ArgumentException("Medya anahtarı geçersiz.", nameof(key));
        var path = Path.GetFullPath(Path.Combine(_root, segments[0], segments[1]));
        var prefix = _root.EndsWith(Path.DirectorySeparatorChar) ? _root : _root + Path.DirectorySeparatorChar;
        if (!path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Medya anahtarı geçersiz.", nameof(key));
        return path;
    }
}
