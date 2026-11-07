using Microsoft.Extensions.Diagnostics.HealthChecks;
using MongoDB.Bson;

namespace Pactogg.Api.Infrastructure.Mongo;

/// <summary>CLARIFICACION #136: si la base está caída, /health/ready reporta fallo (503). F2-D54.</summary>
public sealed class MongoHealthCheck : IHealthCheck
{
    public const string Name = "mongo";
    private readonly MongoContext _mongo;

    public MongoHealthCheck(MongoContext mongo)
    {
        _mongo = mongo;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(3));
            await _mongo.Database.RunCommandAsync<BsonDocument>(new BsonDocument("ping", 1), cancellationToken: timeout.Token);
            return HealthCheckResult.Healthy();
        }
        catch (Exception)
        {
            // Sin detalles en la respuesta: no se exponen cadenas de conexión ni hosts.
            return HealthCheckResult.Unhealthy("database");
        }
    }
}
