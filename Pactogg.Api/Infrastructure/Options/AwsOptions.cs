namespace Pactogg.Api.Infrastructure.Options;

public sealed class AwsOptions
{
    public const string SectionName = "Aws";

    public string Region { get; set; } = "us-east-1";
    public string BucketName { get; set; } = string.Empty;
    public string CloudFrontDomain { get; set; } = string.Empty;
    /// <summary>Obsoleto: la política de imágenes vive en Moderation:Image (F2-D22).</summary>
    public float ModerationConfidenceThreshold { get; set; } = 80f;
}
