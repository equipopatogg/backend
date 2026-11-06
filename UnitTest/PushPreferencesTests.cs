using FluentAssertions;
using Pactogg.Api.Infrastructure.Push;
using Pactogg.Api.Modules.Auth.Domain;

namespace UnitTest;

/// <summary>DEV-03: cada tipo de aviso respeta su preferencia; los de cuenta siempre se envían.</summary>
public sealed class PushPreferencesTests
{
    [Theory]
    [InlineData("contract_received", false, true, true, true, false)]
    [InlineData("contract_resolved", true, false, false, false, true)]
    [InlineData("vote_requested", true, false, true, true, false)]
    [InlineData("vote_expiring", false, true, false, false, true)]
    [InlineData("tournament_started", true, true, false, true, false)]
    [InlineData("commitment_completed", true, true, true, false, false)]
    [InlineData("account_security", false, false, false, false, true)]
    public void Allows_UsesTheCategoryOfTheType(string type, bool retos, bool votos, bool torneos, bool compromisos, bool expected) =>
        PushDispatcher.Allows(new NotificationPreferences { Retos = retos, Votos = votos, Torneos = torneos, Compromisos = compromisos }, type)
            .Should().Be(expected);
}
