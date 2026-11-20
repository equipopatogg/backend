using FluentAssertions;
using Pactogg.Api.Modules.Reputation;

namespace UnitTest;

/// <summary>Spec 008: Índice de Palabra (perfil, Los Tramposos) y catálogo de logros (ACH-02/04).</summary>
public sealed class ReputationRulesTests
{
    [Theory]
    [InlineData(0, 0, 0, 0, 0, 0, 50)]
    [InlineData(3, 0, 0, 2, 0, 4, 70)]
    [InlineData(0, 2, 1, 0, 1, 0, 17)]
    [InlineData(0, 10, 0, 0, 0, 0, 0)]
    [InlineData(30, 0, 0, 0, 0, 50, 100)]
    public void WordIndex_FollowsTheSpecFormulaAndStaysBetween0And100(
        int completed, int cheated, int expired, int onTime, int overdue, int aligned, int expected)
    {
        WordIndexCalculator.Compute(completed, cheated, expired, onTime, overdue, aligned).Should().Be(expected);
    }

    [Fact]
    public void WordIndex_AlignedVotesAddAtMostTenAndNeverSubtract()
    {
        WordIndexCalculator.Compute(0, 0, 0, 0, 0, 500).Should().Be(60);
        WordIndexCalculator.Compute(0, 0, 0, 0, 0, -5).Should().Be(50);
    }

    [Fact]
    public void AchievementCatalog_StreaksAreOrderedAndOnlyTheTrophyRepeatsPerSource()
    {
        AchievementCatalog.Streaks.Select(x => x.Streak).Should().BeInAscendingOrder().And.Equal(3, 5, 10);
        AchievementCatalog.All.Values.Where(x => x.PerSource).Should().ContainSingle()
            .Which.Code.Should().Be("campeon_torneo");
        AchievementCatalog.All.Keys.Should().OnlyHaveUniqueItems();
    }
}
