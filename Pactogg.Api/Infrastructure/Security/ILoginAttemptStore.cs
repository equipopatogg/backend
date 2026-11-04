namespace Pactogg.Api.Infrastructure.Security;

public interface ILoginAttemptStore
{
    Task<int> GetFailuresAsync(string key, CancellationToken cancellationToken);
    Task RecordFailureAsync(string key, TimeSpan ttl, CancellationToken cancellationToken);
    Task ClearAsync(string key, CancellationToken cancellationToken);
}
