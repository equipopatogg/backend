namespace Pactogg.Api.Infrastructure.Moderation;

/// <summary>
/// F2-D76: decisión sobre el resultado de Comprehend para textos de una app de videojuegos.
/// "kills", "matar al jefe", "headshot" o "destruir la base" son vocabulario normal del juego, y Comprehend los marca
/// como VIOLENCE_OR_THREAT y sube la toxicidad general. Por eso esa etiqueta no decide sola: se rechaza por insultos,
/// odio, acoso, contenido sexual, groserías o gráfico, o por toxicidad muy alta que no venga de la violencia.
/// </summary>
public static class TextModerationPolicy
{
    public const float LabelThreshold = 0.7f;
    public const float ToxicityThreshold = 0.9f;
    public const string GameViolence = "VIOLENCE_OR_THREAT";

    public static ModerationResult Evaluate(IEnumerable<(string Name, float Score)> labels, float toxicity)
    {
        var scored = labels.Where(label => !string.IsNullOrWhiteSpace(label.Name)).ToArray();
        var rejected = scored
            .Where(label => label.Score >= LabelThreshold)
            .Where(label => !string.Equals(label.Name, GameViolence, StringComparison.OrdinalIgnoreCase))
            .Select(label => label.Name)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (rejected.Length > 0)
        {
            return ModerationResult.Reject(rejected);
        }

        // La toxicidad general solo rechaza si alguna etiqueta distinta de la violencia del juego también es alta.
        var otherSignal = scored
            .Where(label => !string.Equals(label.Name, GameViolence, StringComparison.OrdinalIgnoreCase))
            .Select(label => label.Score)
            .DefaultIfEmpty(0f)
            .Max();
        return toxicity >= ToxicityThreshold && otherSignal >= 0.5f
            ? ModerationResult.Reject("TOXICITY")
            : ModerationResult.Allow();
    }
}
