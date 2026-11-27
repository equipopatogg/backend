using System.Diagnostics;
using System.Net;
using FluentAssertions;
using IntegrationTest;

namespace PerformanceTest;

/// <summary>
/// Presupuesto de latencia en memoria (sin red ni base de datos reales): detecta regresiones grandes del
/// pipeline (middlewares, límites, serialización) antes de desplegar. La carga real contra AWS se mide con
/// NBomber (<see cref="DeployedLoadTests"/>, workflow calidad-extendida.yml) y no corre en cada PR para no gastar la capa gratuita.
/// </summary>
public sealed class LatencyBudgetTests : IClassFixture<PactoggWebFactory>
{
    private readonly PactoggWebFactory _factory;

    public LatencyBudgetTests(PactoggWebFactory factory) => _factory = factory;

    [Fact]
    public async Task Health_200ConcurrentRequests_P95Under500Ms()
    {
        var client = _factory.CreateClient();
        await client.GetAsync("/health"); // calentamiento

        var timings = await Task.WhenAll(Enumerable.Range(0, 200).Select(async _ =>
        {
            var watch = Stopwatch.StartNew();
            using var response = await client.GetAsync("/health");
            watch.Stop();
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            return watch.Elapsed.TotalMilliseconds;
        }));

        var ordered = timings.OrderBy(x => x).ToArray();
        var p95 = ordered[(int)Math.Ceiling(ordered.Length * 0.95) - 1];
        p95.Should().BeLessThan(500, "el p95 de /health en memoria debe quedar por debajo de 500 ms");
    }
}
