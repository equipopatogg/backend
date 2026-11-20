namespace Pactogg.Api.Infrastructure.Options;

public sealed class RedisOptions
{
    public const string SectionName = "Redis";

    public string ConnectionString { get; set; } = string.Empty;
    public string InstanceName { get; set; } = "pactogg:";

    /// <summary>
    /// Producción: Redis es obligatorio para revocar tokens entre las 2 instancias. Si no responde,
    /// la revisión de revocación falla cerrada (401) en vez de usar memoria local de una sola instancia.
    /// </summary>
    public bool Required { get; set; }
}
