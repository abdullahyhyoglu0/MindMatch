namespace MindMatch.Application.Interfaces;

public interface IPasswordResetEmailSender
{
    Task<bool> SendAsync(string email, string token, CancellationToken cancellationToken = default);
}
