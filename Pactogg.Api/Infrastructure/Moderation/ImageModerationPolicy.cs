using Pactogg.Api.Infrastructure.Options;

namespace Pactogg.Api.Infrastructure.Moderation;

/// <summary>Etiqueta devuelta por el moderador de imágenes (Rekognition DetectModerationLabels).</summary>
public sealed record DetectedLabel(string Name, string? ParentName, float Confidence);

/// <summary>Tipo de contenido detectado (Rekognition ContentTypes: Animated, Illustrated).</summary>
public sealed record DetectedContentType(string Name, float Confidence);

/// <summary>
/// Política de moderación de imágenes para una app de videojuegos (decisión F2-D22, CLARIFICACION #63).
/// Siempre bloquea contenido sexual explícito, símbolos de odio, contenido perturbador y autolesión.
/// La violencia gráfica (sangre, explosiones) se bloquea en fotos reales y se permite en capturas de juego
/// (contenido Animated). Las armas se permiten siempre. Todo lo demás se permite.
/// </summary>
public static class ImageModerationPolicy
{
    // Jerarquía de la taxonomía v7 de Rekognition (L2/L3 -> padre), para ubicar una etiqueta
    // aunque la respuesta solo traiga el nivel inferior.
    private static readonly Dictionary<string, string> KnownParents = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Explicit Nudity"] = "Explicit",
        ["Explicit Sexual Activity"] = "Explicit",
        ["Sex Toys"] = "Explicit",
        ["Exposed Male Genitalia"] = "Explicit Nudity",
        ["Exposed Female Genitalia"] = "Explicit Nudity",
        ["Exposed Buttocks or Anus"] = "Explicit Nudity",
        ["Exposed Female Nipple"] = "Explicit Nudity",
        ["Weapons"] = "Violence",
        ["Graphic Violence"] = "Violence",
        ["Weapon Violence"] = "Graphic Violence",
        ["Physical Violence"] = "Graphic Violence",
        ["Self-Harm"] = "Graphic Violence",
        ["Blood & Gore"] = "Graphic Violence",
        ["Explosions and Blasts"] = "Graphic Violence",
        ["Death and Emaciation"] = "Visually Disturbing",
        ["Emaciated Bodies"] = "Death and Emaciation",
        ["Corpses"] = "Death and Emaciation",
        ["Crashes"] = "Visually Disturbing",
        ["Air Crash"] = "Crashes",
        ["Nazi Party"] = "Hate Symbols",
        ["White Supremacy"] = "Hate Symbols",
        ["Extremist"] = "Hate Symbols"
    };

    public static ModerationResult Evaluate(
        IEnumerable<DetectedLabel> labels,
        IEnumerable<DetectedContentType> contentTypes,
        ImageModerationOptions options)
    {
        var animatedTypes = new HashSet<string>(options.RelaxedContentTypes, StringComparer.OrdinalIgnoreCase);
        var isGameOrDrawing = contentTypes.Any(type =>
            animatedTypes.Contains(type.Name) && type.Confidence >= options.ContentTypeMinConfidence);

        var alwaysBlocked = new HashSet<string>(options.AlwaysBlocked, StringComparer.OrdinalIgnoreCase);
        var blockedUnlessAnimated = new HashSet<string>(options.BlockedUnlessAnimated, StringComparer.OrdinalIgnoreCase);

        var reasons = new List<string>();
        foreach (var label in labels.Where(label => label.Confidence >= options.MinConfidence))
        {
            var lineage = Lineage(label);
            if (lineage.Any(alwaysBlocked.Contains)
                || (!isGameOrDrawing && lineage.Any(blockedUnlessAnimated.Contains)))
            {
                reasons.Add(label.Name);
            }
        }

        return reasons.Count == 0
            ? ModerationResult.Allow()
            : ModerationResult.Reject(reasons.Distinct(StringComparer.OrdinalIgnoreCase).ToArray());
    }

    private static List<string> Lineage(DetectedLabel label)
    {
        var lineage = new List<string> { label.Name };
        var current = string.IsNullOrWhiteSpace(label.ParentName) ? null : label.ParentName;
        if (current is null)
        {
            KnownParents.TryGetValue(label.Name, out current);
        }

        for (var depth = 0; current is not null && depth < 4; depth++)
        {
            lineage.Add(current);
            KnownParents.TryGetValue(current, out current);
        }

        return lineage;
    }
}
