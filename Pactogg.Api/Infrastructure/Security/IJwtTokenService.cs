using Pactogg.Api.Modules.Auth.Domain;

namespace Pactogg.Api.Infrastructure.Security;

public sealed record AccessTokenResult(string Token, DateTime ExpiresAt);

public interface IJwtTokenService
{
    AccessTokenResult CreateAccessToken(User user);
    string CreateRefreshToken();
    string HashRefreshToken(string token);
}
