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
    private readonly ImageModerationOptions _imagePolicy;
    private readonly BlockedTermsMatcher _blockedTerms;

    public AwsModerationService(
        IAmazonRekognition rekognition,
        IAmazonComprehend comprehend,
        IOptions<ModerationOptions> moderationOptions)
    {
        _rekognition = rekognition;
        _comprehend = comprehend;
        _imagePolicy = moderationOptions.Value.Image;
        _blockedTerms = new BlockedTermsMatcher(moderationOptions.Value.BlockedTerms);
    }

    public async Task<ModerationResult> ModerateImageAsync(byte[] image, CancellationToken cancellationToken)
    {
        try
        {
            var response = await _rekognition.DetectModerationLabelsAsync(new DetectModerationLabelsRequest
            {
                Image = new Amazon.Rekognition.Model.Image { Bytes = new MemoryStream(image) },
                // Se pide con umbral bajo y la política filtra; así también llegan los ContentTypes.
                MinConfidence = Math.Min(_imagePolicy.MinConfidence, _imagePolicy.ContentTypeMinConfidence)
            }, cancellationToken);

            var labels = (response.ModerationLabels ?? [])
                .Where(label => !string.IsNullOrWhiteSpace(label.Name))
                .Select(label => new DetectedLabel(label.Name, label.ParentName, label.Confidence ?? 0f));

            var contentTypes = (response.ContentTypes ?? [])
                .Where(type => !string.IsNullOrWhiteSpace(type.Name))
                .Select(type => new DetectedContentType(type.Name, type.Confidence ?? 0f));

            return ImageModerationPolicy.Evaluate(labels, contentTypes, _imagePolicy);
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
        // 007-D3: primero la lista local en español; si coincide no se llama a Comprehend.
        if (_blockedTerms.Find(text).Count > 0)
        {
            return ModerationResult.Reject("BLOCKED_TERM");
        }

        try
        {
            var response = await _comprehend.DetectToxicContentAsync(new DetectToxicContentRequest
            {
                LanguageCode = LanguageCode.En,
                TextSegments = [new TextSegment { Text = text }]
            }, cancellationToken);

            var results = response.ResultList ?? [];
            var labels = results
                .SelectMany(result => result.Labels ?? [])
                .Where(label => label.Name is not null)
                .Select(label => (label.Name.ToString(), label.Score ?? 0f));
            var toxicity = results.Select(result => result.Toxicity ?? 0f).DefaultIfEmpty(0f).Max();

            // F2-D76: VIOLENCE_OR_THREAT no decide sola, porque en una app de videojuegos "kills" es normal.
            return TextModerationPolicy.Evaluate(labels, toxicity);
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
