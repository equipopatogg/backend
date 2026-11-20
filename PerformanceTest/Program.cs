namespace PerformanceTest;

/// <summary>
/// Pruebas de rendimiento: <see cref="LatencyBudgetTests"/> corre en cada PR (presupuesto de latencia en memoria)
/// y <see cref="FunctionalFlowTests"/> verifica el flujo base. La carga contra la URL desplegada usa NBomber.
/// </summary>
internal static class PerformanceSuite
{
    public const string Description = "Presupuesto de latencia por PR y carga con NBomber contra AWS.";
}
