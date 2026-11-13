namespace Pactogg.Api.Infrastructure.Options;

/// <summary>
/// Spec 005 (CON-08). Proveedor del borrador por voz. "None" = sin IA (503 AI_UNAVAILABLE);
/// "OpenAiCompatible" = cualquier API tipo /chat/completions (Groq, OpenRouter, Mistral, Ollama, Gemini en modo OpenAI).
/// La clave va en SSM como Ai__ApiKey, nunca en el repositorio.
/// </summary>
public sealed class AiOptions
{
    public const string SectionName = "Ai";

    public string Provider { get; set; } = "None";
    public string Model { get; set; } = string.Empty;
    public string Endpoint { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public int TimeoutSeconds { get; set; } = 8;
    public int PerMinute { get; set; } = 5;
    public int PerDay { get; set; } = 20;
    public int CacheMinutes { get; set; } = 10;
    public int CircuitFailures { get; set; } = 5;
    public int CircuitOpenSeconds { get; set; } = 60;
}
