namespace MindMatch.Application.Interfaces;

public interface IBackgroundCleanupService
{
    Task ExecuteBatchAsync(CancellationToken cancellationToken = default);
}
