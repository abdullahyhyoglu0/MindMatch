namespace MindMatch.Application.Dtos.Calls;

public sealed record TurnCredentialsResponse(
    IReadOnlyList<string> Urls,
    string Username,
    string Credential,
    DateTime ExpiresAtUtc);
