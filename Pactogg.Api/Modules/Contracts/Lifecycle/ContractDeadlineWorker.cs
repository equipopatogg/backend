using Microsoft.Extensions.Options;

namespace Pactogg.Api.Modules.Contracts.Lifecycle;

/// <summary>Tarea programada (cada 5 min por defecto) que cierra votaciones y retos vencidos.</summary>
public sealed class ContractDeadlineWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly ContractLifecycleOptions _options;
    private readonly ILogger<ContractDeadlineWorker> _logger;

    public ContractDeadlineWorker(IServiceScopeFactory scopes, IOptions<ContractLifecycleOptions> options, ILogger<ContractDeadlineWorker> logger)
    {
        _scopes = scopes;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            return;
        }

        // Desfase aleatorio para que las dos instancias no corran exactamente al mismo tiempo.
        await Task.Delay(TimeSpan.FromSeconds(Random.Shared.Next(5, 60)), stoppingToken);
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(Math.Max(30, _options.IntervalSeconds)));
        do
        {
            try
            {
                using var scope = _scopes.CreateScope();
                var lifecycle = scope.ServiceProvider.GetRequiredService<ContractLifecycleService>();
                var processed = await lifecycle.RunOnceAsync(DateTime.UtcNow, stoppingToken);
                if (processed > 0)
                {
                    _logger.LogInformation("Ciclo de vida: {Count} retos revisados.", processed);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "Falló la pasada del ciclo de vida de retos; se reintenta en el siguiente intervalo.");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
