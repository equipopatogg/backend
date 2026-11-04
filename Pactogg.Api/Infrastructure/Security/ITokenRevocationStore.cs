namespace Pactogg.Api.Infrastructure.Security;

public interface ITokenRevocationStore
{
    Task RevokeAsync(string jti, DateTime expiresAt, CancellationToken cancellationToken);
    Task<bool> IsRevokedAsync(string jti, CancellationToken cancellationToken);
}
