using FluentAssertions;
using Pactogg.Api.Infrastructure.Moderation;

namespace UnitTest;

/// <summary>F2-D76: el vocabulario de videojuegos no se rechaza por violencia; los insultos sí.</summary>
public sealed class TextModerationPolicyTests
{
    [Fact]
    public void GameViolence_AloneWithHighToxicity_IsApproved()
    {
        // "Get 20 kills with the sniper": Comprehend marca violencia alta y sube la toxicidad.
        TextModerationPolicy.Evaluate([("VIOLENCE_OR_THREAT", 0.93f), ("INSULT", 0.04f)], 0.92f)
            .Approved.Should().BeTrue();
    }

    [Theory]
    [InlineData("INSULT")]
    [InlineData("HATE_SPEECH")]
    [InlineData("HARASSMENT_OR_ABUSE")]
    [InlineData("SEXUAL")]
    [InlineData("PROFANITY")]
    public void OtherHighLabels_AreRejected(string label)
    {
        var result = TextModerationPolicy.Evaluate([(label, 0.8f), ("VIOLENCE_OR_THREAT", 0.9f)], 0.95f);
        result.Approved.Should().BeFalse();
        result.Labels.Should().Equal(label);
    }

    [Fact]
    public void VeryHighToxicity_WithAnotherSignal_IsRejected()
    {
        TextModerationPolicy.Evaluate([("INSULT", 0.6f), ("PROFANITY", 0.55f)], 0.95f)
            .Approved.Should().BeFalse();
    }

    [Fact]
    public void CleanText_IsApproved()
    {
        TextModerationPolicy.Evaluate([("INSULT", 0.01f)], 0.02f).Approved.Should().BeTrue();
    }
}
