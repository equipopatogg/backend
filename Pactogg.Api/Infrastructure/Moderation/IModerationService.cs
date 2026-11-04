namespace Pactogg.Api.Infrastructure.Moderation;

public sealed record ModerationResult(bool Approved, IReadOnlyList<string> Labels)
{
    public static ModerationResult Allow() => new(true, []);
    public static ModerationResult Reject(params string[] labels) => new(false, labels);
}

public interface IModerationService
{
    Task<ModerationResult> ModerateImageAsync(byte[] image, CancellationToken cancellationToken);
    Task<ModerationResult> ModerateTextAsync(string text, CancellationToken cancellationToken);
}
