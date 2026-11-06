using FluentAssertions;
using Pactogg.Api.Modules.Contracts.Lifecycle;

namespace UnitTest;

/// <summary>Consenso, quórum y empate (CLARIFICACION #79–83). Pruebas unitarias exigidas por DESC_BACK 12.</summary>
public sealed class VotingRulesTests
{
    [Theory]
    // Con 3 jurados, 2 de 3 es consenso (CLAR #80): 2×100 ≥ 66×3.
    [InlineData(2, 1, 3, 0.66, true, VotingDecision.Fulfilled)]
    [InlineData(1, 2, 3, 0.66, true, VotingDecision.Cheating)]
    // Empate con quórum.
    [InlineData(1, 1, 2, 0.66, true, VotingDecision.Tie)]
    [InlineData(2, 2, 4, 0.50, true, VotingDecision.Tie)]
    // Quórum: al menos la mitad de los asignados. 1 de 3 no alcanza; 2 de 4 sí.
    [InlineData(1, 0, 3, 0.66, true, VotingDecision.NoQuorum)]
    [InlineData(2, 0, 4, 0.66, true, VotingDecision.Fulfilled)]
    [InlineData(0, 0, 3, 0.66, true, VotingDecision.NoQuorum)]
    // Se mide sobre votos emitidos, no sobre asignados: 2 legit de 3 emitidos con 5 asignados.
    [InlineData(2, 1, 5, 0.66, true, VotingDecision.Fulfilled)]
    // Consenso del 100 %: cualquier voto en contra empata.
    [InlineData(2, 1, 3, 1.0, true, VotingDecision.Tie)]
    // Sin jurados decide el administrador.
    [InlineData(0, 0, 0, 0.66, true, VotingDecision.NoQuorum)]
    public void Decide_Final(int legit, int cheat, int jurors, double consensus, bool final, VotingDecision expected) =>
        VotingRules.Decide(legit, cheat, jurors, consensus, final).Should().Be(expected);

    [Theory]
    // Cierre anticipado solo si ya no puede cambiar: 2 de 3 legit aunque falte uno.
    [InlineData(2, 0, 3, VotingDecision.Fulfilled)]
    [InlineData(0, 2, 3, VotingDecision.Cheating)]
    // 1 de 3: los faltantes pueden voltearlo.
    [InlineData(1, 0, 3, VotingDecision.Pending)]
    // 3 de 5 no basta (300 < 330): espera.
    [InlineData(3, 0, 5, VotingDecision.Pending)]
    [InlineData(4, 0, 5, VotingDecision.Fulfilled)]
    // Todos votaron: decide sobre emitidos.
    [InlineData(1, 1, 2, VotingDecision.Tie)]
    public void Decide_WhileVoting(int legit, int cheat, int jurors, VotingDecision expected) =>
        VotingRules.Decide(legit, cheat, jurors, 0.66, final: false).Should().Be(expected);

    [Theory]
    [InlineData(0.66, 66)]
    [InlineData(0.5, 50)]
    [InlineData(1.0, 100)]
    [InlineData(0.2, 50)]
    public void ConsensusPercent_IsClampedBetween50And100(double consensus, int expected) =>
        VotingRules.ConsensusPercent(consensus).Should().Be(expected);
}
