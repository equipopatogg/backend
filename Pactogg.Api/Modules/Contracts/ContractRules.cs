using System.Text.RegularExpressions;
using Pactogg.Api.Common;

namespace Pactogg.Api.Modules.Contracts;

public static partial class ContractRules
{
    [GeneratedRegex(@"(?i)(\b(?:mxn|usd|eur(?:os?)?|pesos?|d[oó]lar(?:es)?)\b|[$€£¥])")]
    private static partial Regex MoneyPattern();

    /// <summary>Spec 005-R10: la recompensa del borrador no puede traer dinero (mismo criterio que CON-01).</summary>
    public static bool ContainsMoney(string value) => MoneyPattern().IsMatch(value);

    public static string NormalizeReward(string reward)
    {
        var value = reward.Trim();
        if (value.Length is < 3 or > 80)
        {
            throw ApiException.Unprocessable(ErrorCodes.ValidationError, "La recompensa debe tener entre 3 y 80 caracteres.");
        }

        if (MoneyPattern().IsMatch(value))
        {
            throw ApiException.Unprocessable(ErrorCodes.ValidationError, "La recompensa no puede incluir dinero ni símbolos monetarios.");
        }

        return value;
    }
}
