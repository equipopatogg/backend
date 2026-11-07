using MongoDB.Bson;
using Pactogg.Api.Common;
using Pactogg.Api.Infrastructure.Audit;
using Pactogg.Api.Modules.Auth.Abstractions;
using Pactogg.Api.Modules.Commitments.Abstractions;
using Pactogg.Api.Modules.Commitments.Domain;
using Pactogg.Api.Modules.Commitments.Dtos;
using Pactogg.Api.Modules.Groups;
using Pactogg.Api.Modules.Notifications;

namespace Pactogg.Api.Modules.Commitments;

/// <summary>COM-01, COM-03, COM-04. Los compromisos se crean solos al cumplirse un reto y nunca se borran.</summary>
public sealed class CommitmentService
{
    private static readonly HashSet<string> ValidStatuses = new(StringComparer.Ordinal) { "pending", "completion_requested", "completed" };

    private readonly ICommitmentRepository _commitments;
    private readonly IUserRepository _users;
    private readonly GroupAccess _access;
    private readonly NotificationService _notifications;
    private readonly IAuditService _audit;

    public CommitmentService(
        ICommitmentRepository commitments,
        IUserRepository users,
        GroupAccess access,
        NotificationService notifications,
        IAuditService audit)
    {
        _commitments = commitments;
        _users = users;
        _access = access;
        _notifications = notifications;
        _audit = audit;
    }

    /// <summary>role = debo (lo que yo debo) o me-deben (lo que me deben).</summary>
    public async Task<(IReadOnlyList<CommitmentResponse> Items, object Meta)> ListAsync(
        ObjectId userId,
        string gid,
        string? role,
        string? status,
        int limit,
        string? cursor,
        CancellationToken cancellationToken)
    {
        ListQuery.ValidateLimit(limit);
        var cursorId = ListQuery.ParseCursor(cursor);
        var normalizedRole = string.IsNullOrWhiteSpace(role) ? "debo" : role.Trim().ToLowerInvariant();
        if (normalizedRole is not ("debo" or "me-deben"))
        {
            throw ApiException.Unprocessable(ErrorCodes.ValidationError, "role debe ser debo o me-deben.");
        }

        if (!string.IsNullOrWhiteSpace(status) && !ValidStatuses.Contains(status))
        {
            throw ApiException.Unprocessable(ErrorCodes.ValidationError, "status no es válido.");
        }

        var (group, _) = await _access.RequireMemberAsync(userId, gid, cancellationToken);
        var (items, hasMore) = await _commitments.ListAsync(group.Id, userId, normalizedRole == "debo", status, limit, cursorId, cancellationToken);
        var next = hasMore && items.Count > 0 ? items[^1].Id.ToString() : null;
        return (await MapAsync(items, cancellationToken), new { next_cursor = next, has_more = hasMore });
    }

    /// <summary>COM-02: detalle para cualquier miembro del grupo (LoadAsync valida pertenencia).</summary>
    public async Task<CommitmentResponse> GetAsync(ObjectId userId, string id, CancellationToken cancellationToken) =>
        (await MapAsync([await LoadAsync(userId, id, cancellationToken)], cancellationToken))[0];

    /// <summary>COM-05: contador de compromisos abiertos del usuario en el grupo.</summary>
    public async Task<CommitmentSummaryResponse> SummaryAsync(ObjectId userId, string gid, CancellationToken cancellationToken)
    {
        var (group, _) = await _access.RequireMemberAsync(userId, gid, cancellationToken);
        return new CommitmentSummaryResponse
        {
            Debo = await _commitments.CountOpenAsync(group.Id, userId, true, cancellationToken),
            MeDeben = await _commitments.CountOpenAsync(group.Id, userId, false, cancellationToken)
        };
    }

    /// <summary>COM-03: el acreedor confirma que ya le cumplieron.</summary>
    public async Task<CommitmentResponse> CompleteAsync(ObjectId userId, string id, CancellationToken cancellationToken)
    {
        var commitment = await LoadAsync(userId, id, cancellationToken);
        if (commitment.CreditorId != userId)
        {
            throw ApiException.Forbidden(ErrorCodes.NotCommitmentParty, "Solo a quien se le debe puede marcarlo como cumplido.");
        }

        var updated = await _commitments.TryTransitionAsync(commitment.Id, ["pending", "completion_requested"], "completed", DateTime.UtcNow, cancellationToken)
            ?? throw ApiException.Conflict(ErrorCodes.InvalidCommitmentState, "El compromiso ya estaba cumplido.");

        await _audit.AppendAsync("commitment.completed", userId, "commitment", updated.Id, updated.GroupId, new { commitment_id = updated.Id.ToString() }, cancellationToken);
        await _notifications.NotifyAsync(
            [updated.DebtorId],
            "commitment_completed",
            "Compromiso saldado",
            $"Se marcó como cumplido: {updated.Description}",
            updated.GroupId,
            updated.ContractId,
            updated.Id,
            cancellationToken);
        return (await MapAsync([updated], cancellationToken))[0];
    }

    /// <summary>COM-04: el deudor avisa que ya cumplió y pide confirmación.</summary>
    public async Task<CommitmentResponse> RequestCompleteAsync(ObjectId userId, string id, CancellationToken cancellationToken)
    {
        var commitment = await LoadAsync(userId, id, cancellationToken);
        if (commitment.DebtorId != userId)
        {
            throw ApiException.Forbidden(ErrorCodes.NotCommitmentParty, "Solo quien debe puede pedir que se marque como cumplido.");
        }

        var updated = await _commitments.TryTransitionAsync(commitment.Id, ["pending"], "completion_requested", DateTime.UtcNow, cancellationToken)
            ?? throw ApiException.Conflict(ErrorCodes.InvalidCommitmentState, "El compromiso no está pendiente.");

        await _audit.AppendAsync("commitment.completion_requested", userId, "commitment", updated.Id, updated.GroupId, new { commitment_id = updated.Id.ToString() }, cancellationToken);
        await _notifications.NotifyAsync(
            [updated.CreditorId],
            "commitment_completion_requested",
            "¿Ya te cumplieron?",
            $"Confirma si ya recibiste: {updated.Description}",
            updated.GroupId,
            updated.ContractId,
            updated.Id,
            cancellationToken);
        return (await MapAsync([updated], cancellationToken))[0];
    }

    private async Task<Commitment> LoadAsync(ObjectId userId, string id, CancellationToken cancellationToken)
    {
        if (!ObjectId.TryParse(id, out var commitmentId))
        {
            throw ApiException.NotFound("El compromiso no existe.");
        }

        var commitment = await _commitments.GetByIdAsync(commitmentId, cancellationToken)
            ?? throw ApiException.NotFound("El compromiso no existe.");
        await _access.RequireMemberAsync(userId, commitment.GroupId, cancellationToken);
        return commitment;
    }

    private async Task<IReadOnlyList<CommitmentResponse>> MapAsync(IReadOnlyList<Commitment> items, CancellationToken cancellationToken)
    {
        var ids = items.SelectMany(x => new[] { x.DebtorId, x.CreditorId }).Distinct().ToArray();
        var users = (await _users.ListByIdsAsync(ids, cancellationToken)).ToDictionary(x => x.Id, x => x.Nickname);
        CommitmentPartyResponse Party(ObjectId id) => new() { UserId = id.ToString(), Nickname = users.GetValueOrDefault(id, "usuario") };

        return items.Select(x => new CommitmentResponse
        {
            Id = x.Id.ToString(),
            GroupId = x.GroupId.ToString(),
            ContractId = x.ContractId.ToString(),
            Debtor = Party(x.DebtorId),
            Creditor = Party(x.CreditorId),
            Description = x.Description,
            Game = x.Game,
            Status = x.Status,
            RequestedAt = x.RequestedAt,
            CompletedAt = x.CompletedAt,
            CreatedAt = x.CreatedAt
        }).ToList();
    }
}
