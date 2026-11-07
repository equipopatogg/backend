namespace Pactogg.Api.Infrastructure.Storage;

public interface IObjectStorage
{
    Task<string> PutAsync(byte[] content, string contentType, string objectKey, CancellationToken cancellationToken);
    Task<string?> GetReadUrlAsync(string objectKey, TimeSpan ttl, CancellationToken cancellationToken);

    Task DeleteAsync(string objectKey, CancellationToken cancellationToken);
}
