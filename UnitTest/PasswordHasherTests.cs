using FluentAssertions;
using Pactogg.Api.Infrastructure.Security;

namespace UnitTest;

public sealed class PasswordHasherTests
{
    private readonly Argon2PasswordHasher _hasher = new();

    [Fact]
    public void Hash_ThenVerify_AcceptsCorrectPassword()
    {
        var hash = _hasher.Hash("frase larga de ejemplo");

        _hasher.Verify(hash, "frase larga de ejemplo").Should().BeTrue();
        hash.Should().StartWith("$argon2id$v=19$");
    }

    [Fact]
    public void Verify_RejectsWrongPassword()
    {
        var hash = _hasher.Hash("frase larga de ejemplo");

        _hasher.Verify(hash, "frase incorrecta").Should().BeFalse();
    }

    [Fact]
    public void Verify_RejectsMalformedHash()
    {
        _hasher.Verify("no-es-un-hash", "frase larga de ejemplo").Should().BeFalse();
    }
}
