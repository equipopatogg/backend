using MongoDB.Bson;
using Pactogg.Api.Modules.Auth.Abstractions;
using Pactogg.Api.Modules.Auth.Domain;
using Pactogg.Api.Modules.Devices;

namespace Pactogg.Api.Infrastructure.Push;

/// <summary>
/// Lee la cola y envía el push respetando las preferencias de cada usuario (DEV-03): retos, votos, torneos
/// y compromisos. Borra los tokens que FCM reporta como inválidos.
/// </summary>
public sealed class PushDispatcher : BackgroundService
{
    private readonly PushQueue _queue;
    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<PushDispatcher> _logger;

    public PushDispatcher(PushQueue queue, IServiceScopeFactory scopes, ILogger<PushDispatcher> logger)
    {
        _queue = queue;
        _scopes = scopes;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var message in _queue.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                using var scope = _scopes.CreateScope();
                await DeliverAsync(scope.ServiceProvider, message, stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "No se pudo enviar un push de tipo {Type}.", message.Type);
            }
        }
    }

    /// <summary>Entrega un mensaje. Público para pruebas.</summary>
    public static async Task<PushResult> DeliverAsync(IServiceProvider services, PushMessage message, CancellationToken cancellationToken)
    {
        var sender = services.GetRequiredService<IPushSender>();
        if (!sender.IsConfigured)
        {
            return new PushResult(0, []);
        }

        var users = services.GetRequiredService<IUserRepository>();
        var devices = services.GetRequiredService<IDeviceRepository>();
        var recipients = (await users.ListByIdsAsync(message.UserIds, cancellationToken))
            .Where(x => x.Status == "active" && Allows(x.NotificationPreferences, message.Type))
            .Select(x => x.Id)
            .ToHashSet();
        if (recipients.Count == 0)
        {
            return new PushResult(0, []);
        }

        var tokens = (await devices.ListByUsersAsync(recipients, cancellationToken)).Select(x => x.FcmToken).Distinct().ToList();
        var result = await sender.SendAsync(tokens, message, cancellationToken);
        await devices.DeleteTokensAsync(result.InvalidTokens, cancellationToken);
        return result;
    }

    /// <summary>Categoría por prefijo del tipo de aviso. Lo que no encaja (cuenta, sistema) siempre se envía.</summary>
    public static bool Allows(NotificationPreferences preferences, string type) => type switch
    {
        _ when type.StartsWith("contract_", StringComparison.Ordinal) => preferences.Retos,
        _ when type.StartsWith("vote_", StringComparison.Ordinal) => preferences.Votos,
        _ when type.StartsWith("tournament_", StringComparison.Ordinal) => preferences.Torneos,
        _ when type.StartsWith("commitment_", StringComparison.Ordinal) => preferences.Compromisos,
        _ => true
    };
}
