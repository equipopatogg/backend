using Microsoft.Extensions.Options;
using Pactogg.Api.Infrastructure.Options;

namespace Pactogg.Api.Infrastructure.Ai;

/// <summary>005-D3: tras 5 fallos seguidos del proveedor el circuito se abre 60 s (local por instancia).</summary>
public sealed class DraftCircuitBreaker
{
    private readonly object _lock = new();
    private readonly AiOptions _options;
    private readonly TimeProvider _time;
    private int _failures;
    private DateTimeOffset? _openUntil;

    public DraftCircuitBreaker(IOptions<AiOptions> options, TimeProvider? time = null)
    {
        _options = options.Value;
        _time = time ?? TimeProvider.System;
    }

    public bool IsOpen
    {
        get
        {
            lock (_lock)
            {
                if (_openUntil is DateTimeOffset until && _time.GetUtcNow() < until)
                {
                    return true;
                }

                if (_openUntil is not null)
                {
                    // Semiabierto: se deja pasar una llamada; si vuelve a fallar se abre otra vez.
                    _openUntil = null;
                    _failures = Math.Max(0, _options.CircuitFailures - 1);
                }

                return false;
            }
        }
    }

    public void RecordSuccess()
    {
        lock (_lock)
        {
            _failures = 0;
            _openUntil = null;
        }
    }

    public void RecordFailure()
    {
        lock (_lock)
        {
            _failures++;
            if (_failures >= _options.CircuitFailures)
            {
                _openUntil = _time.GetUtcNow().AddSeconds(_options.CircuitOpenSeconds);
            }
        }
    }

    public void Reset() => RecordSuccess();
}
