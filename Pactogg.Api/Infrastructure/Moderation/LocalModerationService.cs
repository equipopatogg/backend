using Microsoft.Extensions.Options;
using Pactogg.Api.Infrastructure.Options;

namespace Pactogg.Api.Infrastructure.Moderation;

public sealed class LocalModerationService : IModerationService
{
    private readonly IReadOnlyList<string> _blockedTerms;

    public LocalModerationService(IOptions<ModerationOptions> options)
    {
        _blockedTerms = options.Value.BlockedTerms;
    }

    public Task<ModerationResult> ModerateImageAsync(byte[] image, CancellationToken cancellationToken) =>
        Task.FromResult(ModerationResult.Allow());

    public Task<ModerationResult> ModerateTextAsync(string text, CancellationToken cancellationToken)
    {
        var matches = _blockedTerms
            .Where(term => !string.IsNullOrWhiteSpace(term)
                && text.Contains(term, StringComparison.OrdinalIgnoreCase))
            .ToArray();

        return Task.FromResult(matches.Length == 0
            ? ModerationResult.Allow()
            : ModerationResult.Reject(matches));
    }
}
