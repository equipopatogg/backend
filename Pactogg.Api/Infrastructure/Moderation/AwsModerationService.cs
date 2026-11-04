using Amazon.Comprehend;
using Amazon.Comprehend.Model;
using Amazon.Rekognition;
using Amazon.Rekognition.Model;
using Amazon.Runtime;
using Microsoft.Extensions.Options;
using Pactogg.Api.Common;
using Pactogg.Api.Infrastructure.Options;

namespace Pactogg.Api.Infrastructure.Moderation;

public sealed class AwsModerationService : IModerationService
{
    private readonly IAmazonRekognition _rekognition;
    private readonly IAmazonComprehend _comprehend;
    private readonly AwsOptions _options;

    public AwsModerationService(
        IAmazonRekognition rekognition,
        IAmazonComprehend comprehend,
        IOptions<AwsOptions> options)
    {
        _rekognition = rekognition;
        _comprehend = comprehend;
        _options = options.Value;
    }

    public async Task<ModerationResult> ModerateImageAsync(byte[] image, CancellationToken cancellationToken)
    {
        try
        {
            var response = await _rekognition.DetectModerationLabelsAsync(new DetectModerationLabelsRequest
            {
                Image = new Amazon.Rekognition.Model.Image { Bytes = new MemoryStream(image) },
                MinConfidence = _options.ModerationConfidenceThreshold
            }, cancellationToken);

            var labels = response.ModerationLabels
                .Select(label => label.Name.ToString())
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            return labels.Length == 0 ? ModerationResult.Allow() : ModerationResult.Reject(labels!);
        }
        catch (AmazonServiceException)
        {
            throw ApiException.ServiceUnavailable(ErrorCodes.ModerationUnavailable, "No se pudo verificar la imagen.");
        }
        catch (AmazonClientException)
        {
            throw ApiException.ServiceUnavailable(ErrorCodes.ModerationUnavailable, "No se pudo verificar la imagen.");
        }
    }

    public async Task<ModerationResult> ModerateTextAsync(string text, CancellationToken cancellationToken)
    {
        try
        {
            var response = await _comprehend.DetectToxicContentAsync(new DetectToxicContentRequest
            {
                LanguageCode = LanguageCode.En,
                TextSegments = [new TextSegment { Text = text }]
            }, cancellationToken);

            var labels = response.ResultList
                .SelectMany(result => result.Labels ?? [])
                .Where(label => label.Score >= 0.7f)
                .Select(label => label.Name.ToString())
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            var toxic = response.ResultList.Any(result => result.Toxicity >= 0.7f);
            return !toxic && labels.Length == 0
                ? ModerationResult.Allow()
                : ModerationResult.Reject(labels!);
        }
        catch (AmazonServiceException)
        {
            throw ApiException.ServiceUnavailable(ErrorCodes.ModerationUnavailable, "No se pudo verificar el texto.");
        }
        catch (AmazonClientException)
        {
            throw ApiException.ServiceUnavailable(ErrorCodes.ModerationUnavailable, "No se pudo verificar el texto.");
        }
    }
}
