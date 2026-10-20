namespace MindMatch.Api.Services;

public sealed class TurnSettings
{
    public string Provider { get; set; } = "Cloudflare";
    public string[] Urls { get; set; } = [];
    public string SharedSecret { get; set; } = string.Empty;
    public string CloudflareTurnKeyId { get; set; } = string.Empty;
    public string CloudflareApiToken { get; set; } = string.Empty;
    public int CredentialLifetimeSeconds { get; set; } = 600;
}
