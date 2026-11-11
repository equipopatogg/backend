using Pactogg.Api.Infrastructure.Audit;
using Pactogg.Api.Infrastructure.Redis;
using Pactogg.Api.Infrastructure.Storage;
using Pactogg.Api.Modules.Commitments.Abstractions;
using Pactogg.Api.Modules.Contracts.Abstractions;
using Pactogg.Api.Modules.Evidence.Abstractions;
using Pactogg.Api.Modules.Notifications;
using Pactogg.Api.Modules.Reputation;

namespace Pactogg.Api.Modules.Maintenance;

/// <summary>
/// Tareas periódicas que corren junto al ciclo de vida de los retos (ContractDeadlineWorker):
/// borrado de fotos a los 30 días fuera del Muro de la Fama (spec 007-R10) y aviso de compromiso vencido a los 7 días
/// (specs 006-R15 y 009-R4). Todas las escrituras son condicionales, así que dos instancias no duplican efectos.
/// </summary>
public sealed class MaintenanceService
{
    private const int BatchSize = 100;

    private readonly IEvidenceRepository _evidence;
    private readonly IContractRepository _contracts;
    private readonly IObjectStorage _storage;
    private readonly ICommitmentRepository _commitments;
    private readonly ReputationService _reputation;
    private readonly NotificationService _notifications;
    private readonly IAuditService _audit;
    private readonly ICacheService _cache;

    public MaintenanceService(
        IEvidenceRepository evidence,
        IContractRepository contracts,
        IObjectStorage storage,
        ICommitmentRepository commitments,
        ReputationService reputation,
        NotificationService notifications,
        IAuditService audit,
        ICacheService cache)
    {
        _evidence = evidence;
        _contracts = contracts;
        _storage = storage;
        _commitments = commitments;
        _reputation = reputation;
        _notifications = notifications;
        _audit = audit;
        _cache = cache;
    }

    public async Task<int> RunOnceAsync(DateTime now, CancellationToken cancellationToken) =>
        await ExpirePhotosAsync(now, cancellationToken) + await FlagOverdueCommitmentsAsync(now, cancellationToken);

    /// <summary>007-R10: la foto vencida se deja de mostrar y se borra de S3 (la regla del bucket es el respaldo).</summary>
    public async Task<int> ExpirePhotosAsync(DateTime now, CancellationToken cancellationToken)
    {
        var photos = await _evidence.ListExpiredPhotosAsync(now, BatchSize, cancellationToken);
        foreach (var photo in photos)
        {
            await _evidence.MarkExpiredAsync(photo.Id, now, cancellationToken);
            if (!string.IsNullOrWhiteSpace(photo.ObjectKey))
            {
                await _storage.DeleteAsync(photo.ObjectKey, cancellationToken);
            }

            var contract = await _contracts.GetByIdAsync(photo.ContractId, cancellationToken);
            if (contract is not null)
            {
                await _cache.BumpVersionAsync(CacheExtensions.GroupScope(contract.GroupId), cancellationToken);
            }
        }

        return photos.Count;
    }

    /// <summary>009-R4: compromiso con más de 7 días pendiente → un aviso al deudor y cuenta para el Índice.</summary>
    public async Task<int> FlagOverdueCommitmentsAsync(DateTime now, CancellationToken cancellationToken)
    {
        var candidates = await _commitments.ListOverdueCandidatesAsync(now - ReputationService.CommitmentGrace, BatchSize, cancellationToken);
        var flagged = 0;
        foreach (var candidate in candidates)
        {
            var commitment = await _commitments.TryMarkOverdueAsync(candidate.Id, now, cancellationToken);
            if (commitment is null)
            {
                continue;
            }

            flagged++;
            await _reputation.OnCommitmentOverdueAsync(commitment, now, cancellationToken);
            await _audit.AppendAsync("commitment.overdue", null, "commitment", commitment.Id, commitment.GroupId, new { commitment_id = commitment.Id.ToString() }, cancellationToken);
            await _notifications.NotifyAsync(
                [commitment.DebtorId],
                "commitment_overdue",
                "Compromiso por cumplir",
                $"Ya pasó una semana: todavía debes «{commitment.Description}».",
                commitment.GroupId,
                null,
                commitment.Id,
                cancellationToken);
        }

        return flagged;
    }
}
