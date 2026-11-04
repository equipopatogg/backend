namespace Pactogg.Api.Infrastructure.Options;

public sealed class EvidenceOptions
{
    public const string SectionName = "Evidence";

    public List<string> AllowedHosts { get; set; } =
    [
        "youtube.com",
        "youtu.be",
        "twitch.tv",
        "clips.twitch.tv",
        "medal.tv",
        "streamable.com"
    ];

    public long MaxImageBytes { get; set; } = 5 * 1024 * 1024;
    public int RetentionDays { get; set; } = 30;
    public int MaxPerContract { get; set; } = 3;
}
