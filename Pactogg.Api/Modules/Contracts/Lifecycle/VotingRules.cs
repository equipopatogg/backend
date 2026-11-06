namespace Pactogg.Api.Modules.Contracts.Lifecycle;

public enum VotingDecision
{
    /// <summary>Aún faltan votos y nadie alcanzó consenso irreversible.</summary>
    Pending,
    Fulfilled,
    Cheating,
    /// <summary>Hubo quórum pero ningún lado llegó al consenso.</summary>
    Tie,
    /// <summary>Votó menos de la mitad de los jurados asignados.</summary>
    NoQuorum
}

/// <summary>
/// Reglas de consenso (CLARIFICACION #79–83, DESC_BACK 7.6). Sin redondeo: se compara
/// votos_del_lado × 100 ≥ porcentaje × votos_emitidos, con quórum de al menos la mitad de los jurados asignados.
/// </summary>
public static class VotingRules
{
    public static int ConsensusPercent(double consensus) =>
        Math.Clamp((int)Math.Round(consensus * 100, MidpointRounding.AwayFromZero), 50, 100);

    /// <param name="final">true cuando ya votaron todos o venció el plazo: se decide con los votos emitidos.</param>
    public static VotingDecision Decide(int legit, int cheat, int jurorsAssigned, double consensus, bool final)
    {
        var percent = ConsensusPercent(consensus);
        var cast = legit + cheat;

        if (jurorsAssigned <= 0)
        {
            return final ? VotingDecision.NoQuorum : VotingDecision.Pending;
        }

        if (!final && cast < jurorsAssigned)
        {
            // Cierre anticipado solo si el resultado ya no puede cambiar aunque los faltantes voten en contra.
            var legitLocked = legit * 100 >= percent * jurorsAssigned;
            var cheatLocked = cheat * 100 >= percent * jurorsAssigned;
            if (legitLocked && !cheatLocked)
            {
                return VotingDecision.Fulfilled;
            }

            if (cheatLocked && !legitLocked)
            {
                return VotingDecision.Cheating;
            }

            return VotingDecision.Pending;
        }

        if (cast == 0 || cast * 2 < jurorsAssigned)
        {
            return VotingDecision.NoQuorum;
        }

        var legitOk = legit * 100 >= percent * cast;
        var cheatOk = cheat * 100 >= percent * cast;
        if (legitOk && !cheatOk)
        {
            return VotingDecision.Fulfilled;
        }

        if (cheatOk && !legitOk)
        {
            return VotingDecision.Cheating;
        }

        return VotingDecision.Tie;
    }
}
