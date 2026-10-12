namespace FinAI.Api.Contracts;

public sealed record CredentialsRequest(string UserName, string Password);

public sealed record UserResponse(string Id, string UserName);

public sealed record CsrfTokenResponse(string RequestToken);
