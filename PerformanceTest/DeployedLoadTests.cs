using FluentAssertions;
using NBomber.CSharp;

namespace PerformanceTest;

/// <summary>
/// Carga real con NBomber contra la API desplegada en AWS. Solo corre cuando existe
/// <c>PACTOGG_LOAD_BASE_URL</c> (workflow <c>calidad-extendida.yml</c>); en cada PR se omite para no gastar
/// la capa gratuita. Usa <c>/health</c> (público, sin datos personales) y queda por debajo del límite de
/// peticiones por IP (90 peticiones en 60 s) para que un 429 no se confunda con lentitud.
/// </summary>
public sealed class DeployedLoadTests
{
    [LoadFact]
    [Trait("Category", "Load")]
    public void Health_DeployedApi_P95Under800MsWithoutErrors()
    {
        var baseUrl = Environment.GetEnvironmentVariable(LoadFactAttribute.Variable)!.TrimEnd('/');
        using var http = new HttpClient { BaseAddress = new Uri(baseUrl), Timeout = TimeSpan.FromSeconds(10) };

        var scenario = Scenario.Create("health", async _ =>
            {
                using var response = await http.GetAsync("/health");
                return response.IsSuccessStatusCode ? Response.Ok() : Response.Fail();
            })
            .WithoutWarmUp()
            .WithLoadSimulations(Simulation.Inject(rate: 3, interval: TimeSpan.FromSeconds(2), during: TimeSpan.FromSeconds(60)));

        var result = NBomberRunner.RegisterScenarios(scenario)
            .WithReportFolder("nbomber-report")
            .Run();

        var stats = result.ScenarioStats.First(s => s.ScenarioName == "health");
        stats.Fail.Request.Count.Should().Be(0, "ninguna petición debe fallar ni recibir 429");
        stats.Ok.Request.Count.Should().BeGreaterThan(0);
        stats.Ok.Latency.Percent95.Should().BeLessThan(800, "p95 de /health desplegado por debajo de 800 ms");
    }
}

/// <summary>Se omite si no hay URL de la API desplegada.</summary>
public sealed class LoadFactAttribute : FactAttribute
{
    public const string Variable = "PACTOGG_LOAD_BASE_URL";

    public LoadFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(Variable)))
        {
            Skip = $"Sin {Variable}: la carga contra AWS corre en calidad-extendida.yml.";
        }
    }
}
