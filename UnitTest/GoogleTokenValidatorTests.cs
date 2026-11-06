using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Pactogg.Api.Common;
using Pactogg.Api.Infrastructure.Options;
using Pactogg.Api.Infrastructure.Security;

namespace UnitTest;

public sealed class GoogleTokenValidatorTests
{
    [Fact]
    public async Task WithoutClientIds_FailsClosedWith503()
    {
        var validator = new GoogleTokenValidator(Options.Create(new AuthOptions()), NullLogger<GoogleTokenValidator>.Instance);

        var act = () => validator.ValidateAsync("cualquier.token.firmado", CancellationToken.None);

        var error = await act.Should().ThrowAsync<ApiException>();
        error.Which.StatusCode.Should().Be(503);
        error.Which.Code.Should().Be(ErrorCodes.GoogleAuthUnavailable);
    }
}
