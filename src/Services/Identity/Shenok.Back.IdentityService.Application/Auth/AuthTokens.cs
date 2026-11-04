namespace Shenok.Back.IdentityService.Application.Auth;

public sealed record AuthTokens(string AccessToken, string RefreshToken, DateTime AccessTokenExpiresAt);
