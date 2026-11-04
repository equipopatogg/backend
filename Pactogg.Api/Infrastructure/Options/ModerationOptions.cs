namespace Pactogg.Api.Infrastructure.Options;

public sealed class ModerationOptions
{
    public const string SectionName = "Moderation";

    public string Mode { get; set; } = "Local";
    public List<string> BlockedTerms { get; set; } = [];
}
