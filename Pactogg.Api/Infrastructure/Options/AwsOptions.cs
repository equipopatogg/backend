namespace Pactogg.Api.Infrastructure.Options;

public sealed class AwsOptions
{
    public const string SectionName = "Aws";

    public string Region { get; set; } = "us-east-1";
    public string BucketName { get; set; } = string.Empty;
    public string CloudFrontDomain { get; set; } = string.Empty;
    public float ModerationConfidenceThreshold { get; set; } = 70f;
}
