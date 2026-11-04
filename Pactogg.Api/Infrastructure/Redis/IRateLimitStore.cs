namespace Pactogg.Api.Infrastructure.Redis;

public interface IRateLimitStore
{
    Task<long> IncrementAsync(string key, TimeSpan window, CancellationToken cancellationToken);
}
