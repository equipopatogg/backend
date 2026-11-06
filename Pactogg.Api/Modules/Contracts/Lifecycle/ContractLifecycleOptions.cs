namespace Pactogg.Api.Modules.Contracts.Lifecycle;

public sealed class ContractLifecycleOptions
{
    public const string SectionName = "ContractLifecycle";

    /// <summary>Tarea programada que cierra votaciones y retos vencidos (DESC_BACK 7.6).</summary>
    public bool Enabled { get; set; } = true;

    public int IntervalSeconds { get; set; } = 300;

    public int BatchSize { get; set; } = 100;

    public int VotingHours { get; set; } = 48;

    public int ExtensionHours { get; set; } = 24;

    public int AdminDecisionHours { get; set; } = 48;

    /// <summary>Aviso de "votación por vencer" a los jurados que no han votado.</summary>
    public int ReminderHoursBefore { get; set; } = 6;
}
