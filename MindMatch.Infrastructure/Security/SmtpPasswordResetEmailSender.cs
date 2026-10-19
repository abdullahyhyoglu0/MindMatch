using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MindMatch.Application.Interfaces;

namespace MindMatch.Infrastructure.Security;

public sealed class SmtpPasswordResetEmailSender : IPasswordResetEmailSender
{
    private readonly PasswordResetEmailSettings _settings;
    private readonly ILogger<SmtpPasswordResetEmailSender> _logger;

    public SmtpPasswordResetEmailSender(IOptions<PasswordResetEmailSettings> settings,
        ILogger<SmtpPasswordResetEmailSender> logger)
    { _settings = settings.Value; _logger = logger; }

    public async Task<bool> SendAsync(string email, string token, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_settings.Host) || string.IsNullOrWhiteSpace(_settings.From) ||
            string.IsNullOrWhiteSpace(_settings.ResetUrl))
        {
            _logger.LogWarning("Password reset email was not sent because SMTP delivery is not configured.");
            return false;
        }

        var builder = new UriBuilder(_settings.ResetUrl);
        var query = builder.Query.TrimStart('?');
        builder.Query = string.IsNullOrEmpty(query)
            ? $"token={Uri.EscapeDataString(token)}"
            : $"{query}&token={Uri.EscapeDataString(token)}";
        var resetUrl = builder.Uri.ToString();
        using var message = new MailMessage(_settings.From, email)
        {
            Subject = "MindMatch parola sıfırlama",
            Body = $"Parolanızı yenilemek için bu bağlantıyı kullanın. Bağlantı 30 dakika içinde geçersiz olur:\n\n{resetUrl}",
            IsBodyHtml = false
        };
        using var client = new SmtpClient(_settings.Host, _settings.Port)
        {
            EnableSsl = _settings.EnableSsl,
            Credentials = string.IsNullOrWhiteSpace(_settings.UserName)
                ? CredentialCache.DefaultNetworkCredentials
                : new NetworkCredential(_settings.UserName, _settings.Password)
        };

        try
        {
            await client.SendMailAsync(message).WaitAsync(cancellationToken);
            return true;
        }
        catch (Exception exception) when (exception is SmtpException or OperationCanceledException)
        {
            _logger.LogError(exception, "Password reset email delivery failed.");
            return false;
        }
    }
}
