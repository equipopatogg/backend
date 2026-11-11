using Microsoft.Extensions.Options;
using Pactogg.Api.Infrastructure.Options;

namespace Pactogg.Api.Infrastructure.Moderation;

public sealed class LocalModerationService : IModerationService
{
    private readonly BlockedTermsMatcher _blockedTerms;

    public LocalModerationService(IOptions<ModerationOptions> options)
    {
        _blockedTerms = new BlockedTermsMatcher(options.Value.BlockedTerms);
    }

    public Task<ModerationResult> ModerateImageAsync(byte[] image, CancellationToken cancellationToken) =>
        Task.FromResult(ModerationResult.Allow());

    public Task<ModerationResult> ModerateTextAsync(string text, CancellationToken cancellationToken)
    {
        return Task.FromResult(_blockedTerms.Find(text).Count == 0
            ? ModerationResult.Allow()
            : ModerationResult.Reject("BLOCKED_TERM"));
    }
}
