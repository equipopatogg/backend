using MongoDB.Bson;
using Pactogg.Api.Modules.Tournaments.Domain;

namespace Pactogg.Api.Modules.Tournaments;

/// <summary>
/// 010-R7: eliminación directa. Sortea el orden, completa con byes hasta la siguiente potencia de 2 y crea todas
/// las partidas enlazadas con next_match_id. Los byes avanzan solos a la siguiente ronda.
/// Como el número de grupos siempre es mayor que la mitad del tamaño del bracket, cada bye cae en una partida
/// distinta y nunca hay una partida de bye contra bye.
/// </summary>
public static class BracketBuilder
{
    public static int SizeFor(int groups)
    {
        if (groups < 2)
        {
            throw new ArgumentOutOfRangeException(nameof(groups), "Se necesitan al menos 2 grupos.");
        }

        var size = 2;
        while (size < groups)
        {
            size *= 2;
        }

        return size;
    }

    public static IReadOnlyList<Match> Build(ObjectId tournamentId, IReadOnlyList<ObjectId> groups, Random random, DateTime now)
    {
        var size = SizeFor(groups.Count);
        var order = groups.ToArray();
        for (var i = order.Length - 1; i > 0; i--)
        {
            var j = random.Next(i + 1);
            (order[i], order[j]) = (order[j], order[i]);
        }

        var rounds = new List<List<Match>>();
        for (int round = 1, count = size / 2; count >= 1; round++, count /= 2)
        {
            rounds.Add(Enumerable.Range(0, count).Select(position => new Match
            {
                Id = ObjectId.GenerateNewId(),
                TournamentId = tournamentId,
                Round = round,
                Position = position,
                Status = "pending",
                UpdatedAt = now
            }).ToList());
        }

        for (var r = 0; r < rounds.Count - 1; r++)
        {
            foreach (var match in rounds[r])
            {
                match.NextMatchId = rounds[r + 1][match.Position / 2].Id;
            }
        }

        var half = size / 2;
        foreach (var match in rounds[0])
        {
            match.P1GroupId = order[match.Position];
            var second = match.Position + half;
            match.P2GroupId = second < order.Length ? order[second] : null;
            if (match.P2GroupId is null)
            {
                match.Status = "bye";
                match.WinnerGroupId = match.P1GroupId;
                Advance(rounds, match);
            }
        }

        return rounds.SelectMany(x => x).ToList();
    }

    /// <summary>Coloca al ganador en su lugar de la siguiente partida (posición par → p1, impar → p2).</summary>
    public static void PlaceWinner(Match next, Match from)
    {
        if (from.Position % 2 == 0)
        {
            next.P1GroupId = from.WinnerGroupId;
        }
        else
        {
            next.P2GroupId = from.WinnerGroupId;
        }
    }

    private static void Advance(List<List<Match>> rounds, Match from)
    {
        if (from.Round >= rounds.Count)
        {
            return;
        }

        PlaceWinner(rounds[from.Round][from.Position / 2], from);
    }
}
