using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Options;
using Pactogg.Api.Common;
using Pactogg.Api.Infrastructure.Options;

namespace Pactogg.Api.Infrastructure.Storage;

public sealed class S3ObjectStorage : IObjectStorage
{
    private readonly IAmazonS3 _s3;
    private readonly AwsOptions _options;

    public S3ObjectStorage(IAmazonS3 s3, IOptions<AwsOptions> options)
    {
        _s3 = s3;
        _options = options.Value;
    }

    public async Task<string> PutAsync(byte[] content, string contentType, string objectKey, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_options.BucketName))
        {
            throw ApiException.ServiceUnavailable(ErrorCodes.StorageUnavailable, "Almacenamiento no configurado.");
        }

        try
        {
            await using var stream = new MemoryStream(content, writable: false);
            await _s3.PutObjectAsync(new PutObjectRequest
            {
                BucketName = _options.BucketName,
                Key = objectKey,
                InputStream = stream,
                ContentType = contentType,
                AutoCloseStream = false
            }, cancellationToken);

            return objectKey;
        }
        catch (AmazonS3Exception)
        {
            throw ApiException.ServiceUnavailable(ErrorCodes.StorageUnavailable, "No se pudo guardar la evidencia.");
        }
        catch (AmazonClientException)
        {
            throw ApiException.ServiceUnavailable(ErrorCodes.StorageUnavailable, "No se pudo guardar la evidencia.");
        }
    }

    public Task<string?> GetReadUrlAsync(string objectKey, TimeSpan ttl, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(objectKey))
        {
            return Task.FromResult<string?>(null);
        }

        if (!string.IsNullOrWhiteSpace(_options.CloudFrontDomain))
        {
            return Task.FromResult<string?>($"https://{_options.CloudFrontDomain.TrimEnd('/')}/{objectKey}");
        }

        if (string.IsNullOrWhiteSpace(_options.BucketName))
        {
            return Task.FromResult<string?>(null);
        }

        try
        {
            var url = _s3.GetPreSignedURL(new GetPreSignedUrlRequest
            {
                BucketName = _options.BucketName,
                Key = objectKey,
                Verb = HttpVerb.GET,
                Expires = DateTime.UtcNow.Add(ttl)
            });
            return Task.FromResult<string?>(url);
        }
        catch (AmazonS3Exception)
        {
            return Task.FromResult<string?>(null);
        }
    }
}
