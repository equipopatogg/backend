using FluentAssertions;
using Pactogg.Api.Common;
using Pactogg.Api.Modules.Contracts;

namespace UnitTest;

public sealed class ContractRulesTests
{
    [Theory]
    [InlineData("Elegir el próximo juego")]
    [InlineData("Una skin del pase")]
    [InlineData("Cena del grupo")]
    public void NormalizeReward_AcceptsNonMonetaryRewards(string reward)
    {
        ContractRules.NormalizeReward($"  {reward}  ").Should().Be(reward);
    }

    [Theory]
    [InlineData("$50")]
    [InlineData("50 USD")]
    [InlineData("100 pesos")]
    [InlineData("20 euros")]
    [InlineData("ab")]
    [InlineData("Esta recompensa es demasiado larga para el límite permitido y debe ser rechazada por el sistema")]
    public void NormalizeReward_RejectsMoneyOrInvalidLength(string reward)
    {
        var action = () => ContractRules.NormalizeReward(reward);

        action.Should().Throw<ApiException>().Which.Code.Should().Be(ErrorCodes.ValidationError);
    }
}
