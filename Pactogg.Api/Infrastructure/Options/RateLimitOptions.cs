namespace Pactogg.Api.Infrastructure.Options;

public sealed class RateLimitOptions
{
    public const string SectionName = "RateLimit";

    public bool Enabled { get; set; } = true;
    public int DefaultPermitLimit { get; set; } = 120;
    public int DefaultWindowSeconds { get; set; } = 60;
    public int LoginPermitLimit { get; set; } = 10;
    public int LoginWindowSeconds { get; set; } = 60;
    public int RegisterPermitLimit { get; set; } = 5;
    public int RegisterWindowSeconds { get; set; } = 3600;
    public int EvidencePermitLimit { get; set; } = 10;
    public int EvidenceWindowSeconds { get; set; } = 60;
}
