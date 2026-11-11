namespace Pactogg.Api.Infrastructure.Storage;

public interface IObjectStorage
{
    Task<string> PutAsync(byte[] content, string contentType, string objectKey, CancellationToken cancellationToken);
    Task<string?> GetReadUrlAsync(string objectKey, TimeSpan ttl, CancellationToken cancellationToken);

    Task DeleteAsync(string objectKey, CancellationToken cancellationToken);

    /// <summary>EVI-06: copia el objeto a otra llave dentro del mismo bucket (la foto pasa a fame/ y sale de la regla de 30 días).</summary>
    Task CopyAsync(string sourceKey, string destinationKey, CancellationToken cancellationToken);
}
