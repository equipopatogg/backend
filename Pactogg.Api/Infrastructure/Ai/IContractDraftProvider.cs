namespace Pactogg.Api.Infrastructure.Ai;

/// <summary>
/// Spec 005 §5: adaptador del modelo de IA. Recibe la instrucción del sistema y los datos del usuario por separado
/// (005-R4) y devuelve el texto crudo del modelo, que el servicio trata como no confiable (005-R5).
/// Para conectar otro proveedor solo se agrega una implementación y su configuración (Q-007).
/// </summary>
public interface IContractDraftProvider
{
    /// <summary>false si falta configuración: el servicio responde 503 AI_UNAVAILABLE sin llamar.</summary>
    bool IsConfigured { get; }

    Task<string> CompleteAsync(string systemPrompt, string userData, CancellationToken cancellationToken);
}

/// <summary>Sin proveedor configurado (Ai:Provider=None): la app ofrece escribir el reto a mano.</summary>
public sealed class UnavailableDraftProvider : IContractDraftProvider
{
    public bool IsConfigured => false;

    public Task<string> CompleteAsync(string systemPrompt, string userData, CancellationToken cancellationToken) =>
        throw new InvalidOperationException("No hay proveedor de IA configurado.");
}
