using FluentAssertions;
using Pactogg.Api.Common;
using Pactogg.Api.Modules.Evidence;

namespace UnitTest;

public sealed class EvidenceUrlValidatorTests
{
    private static readonly string[] AllowedHosts = ["youtube.com", "youtu.be", "clips.twitch.tv"];

    [Theory]
    [InlineData("https://youtube.com/watch?v=1")]
    [InlineData("https://www.youtube.com/watch?v=1")]
    [InlineData("https://m.youtube.com/watch?v=1")]
    [InlineData("https://youtu.be/abc")]
    [InlineData("https://clips.twitch.tv/abc")]
    public void Validate_AcceptsAllowedHttpsHosts(string url)
    {
        EvidenceUrlValidator.Validate(url, AllowedHosts).Should().NotBeNull();
    }

    [Theory]
    [InlineData("http://youtube.com/watch?v=1")]
    [InlineData("https://evil.example/video")]
    [InlineData("https://user:pass@youtube.com/watch?v=1")]
    [InlineData("https://youtube.com:8443/watch?v=1")]
    [InlineData("https://youtube.com.evil.example/watch?v=1")]
    [InlineData("javascript:alert(1)")]
    public void Validate_RejectsUnsafeUrls(string url)
    {
        var action = () => EvidenceUrlValidator.Validate(url, AllowedHosts);

        action.Should().Throw<ApiException>().Which.Code.Should().Be(ErrorCodes.InvalidEvidenceUrl);
    }
}
