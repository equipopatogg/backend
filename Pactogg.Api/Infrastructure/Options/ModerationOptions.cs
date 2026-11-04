namespace Pactogg.Api.Infrastructure.Options;

public sealed class ModerationOptions
{
    public const string SectionName = "Moderation";

    public string Mode { get; set; } = "Local";
    public List<string> BlockedTerms { get; set; } = [];
    public ImageModerationOptions Image { get; set; } = new();
}

/// <summary>Política de imágenes (F2-D22). Valores por defecto según CLARIFICACION #63.</summary>
public sealed class ImageModerationOptions
{
    /// <summary>Confianza mínima (0-100) para que una etiqueta cuente. CLARIFICACION #63: 80.</summary>
    public float MinConfidence { get; set; } = 80f;

    /// <summary>Confianza mínima para considerar la imagen una captura de juego o dibujo.</summary>
    public float ContentTypeMinConfidence { get; set; } = 70f;

    /// <summary>Tipos de contenido de Rekognition que relajan la violencia gráfica.</summary>
    public List<string> RelaxedContentTypes { get; set; } = ["Animated", "Illustrated"];

    /// <summary>Categorías bloqueadas siempre (cualquier nivel de la taxonomía).</summary>
    public List<string> AlwaysBlocked { get; set; } =
        ["Explicit", "Explicit Nudity", "Hate Symbols", "Visually Disturbing", "Self-Harm"];

    /// <summary>Categorías bloqueadas en fotos reales y permitidas en capturas de juego.</summary>
    public List<string> BlockedUnlessAnimated { get; set; } = ["Graphic Violence"];
}
