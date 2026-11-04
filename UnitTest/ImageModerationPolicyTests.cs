using FluentAssertions;
using Pactogg.Api.Infrastructure.Moderation;
using Pactogg.Api.Infrastructure.Options;

namespace UnitTest;

/// <summary>F2-D22: política de moderación de imágenes para capturas de videojuegos.</summary>
public sealed class ImageModerationPolicyTests
{
    private static readonly ImageModerationOptions Options = new();
    private static readonly DetectedContentType[] Game = [new("Animated", 95f)];
    private static readonly DetectedContentType[] RealPhoto = [];

    private static ModerationResult Evaluate(DetectedContentType[] types, params DetectedLabel[] labels) =>
        ImageModerationPolicy.Evaluate(labels, types, Options);

    [Fact]
    public void GameScreenshot_WithWeapons_IsApproved()
    {
        Evaluate(Game, new DetectedLabel("Weapons", "Violence", 97f), new DetectedLabel("Violence", null, 97f))
            .Approved.Should().BeTrue();
    }

    [Fact]
    public void GameScreenshot_WithBloodAndExplosions_IsApproved()
    {
        Evaluate(Game,
                new DetectedLabel("Blood & Gore", "Graphic Violence", 91f),
                new DetectedLabel("Explosions and Blasts", "Graphic Violence", 88f))
            .Approved.Should().BeTrue();
    }

    [Fact]
    public void RealPhoto_WithWeapons_IsApproved()
    {
        Evaluate(RealPhoto, new DetectedLabel("Weapons", "Violence", 90f)).Approved.Should().BeTrue();
    }

    [Fact]
    public void RealPhoto_WithGore_IsRejected()
    {
        var result = Evaluate(RealPhoto, new DetectedLabel("Blood & Gore", "Graphic Violence", 92f));

        result.Approved.Should().BeFalse();
        result.Labels.Should().Contain("Blood & Gore");
    }

    [Fact]
    public void RealPhoto_WithGoreWithoutParent_IsRejectedUsingKnownTaxonomy()
    {
        Evaluate(RealPhoto, new DetectedLabel("Blood & Gore", null, 92f)).Approved.Should().BeFalse();
    }

    [Theory]
    [InlineData("Exposed Female Nipple", "Explicit Nudity")]
    [InlineData("Explicit Sexual Activity", "Explicit")]
    [InlineData("Nazi Party", "Hate Symbols")]
    [InlineData("Corpses", "Death and Emaciation")]
    [InlineData("Self-Harm", "Graphic Violence")]
    public void GameScreenshot_WithAlwaysBlockedContent_IsRejected(string name, string parent)
    {
        Evaluate(Game, new DetectedLabel(name, parent, 90f)).Approved.Should().BeFalse();
    }

    [Fact]
    public void LabelBelowMinConfidence_IsIgnored()
    {
        Evaluate(RealPhoto, new DetectedLabel("Blood & Gore", "Graphic Violence", 75f)).Approved.Should().BeTrue();
    }

    [Fact]
    public void AnimatedWithLowConfidence_IsTreatedAsRealPhoto()
    {
        Evaluate([new DetectedContentType("Animated", 40f)], new DetectedLabel("Blood & Gore", "Graphic Violence", 92f))
            .Approved.Should().BeFalse();
    }

    [Theory]
    [InlineData("Middle Finger", "Rude Gestures")]
    [InlineData("Alcoholic Beverages", "Alcohol")]
    [InlineData("Gambling", null)]
    [InlineData("Female Swimwear or Underwear", "Swimwear or Underwear")]
    public void PermittedCategories_AreApproved(string name, string? parent)
    {
        Evaluate(RealPhoto, new DetectedLabel(name, parent, 95f)).Approved.Should().BeTrue();
    }
}
