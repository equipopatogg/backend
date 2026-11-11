using Pactogg.Api.Modules.Auth.Abstractions;
using Pactogg.Api.Modules.Contracts.Lifecycle;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using Pactogg.Api.Common;
using Pactogg.Api.Infrastructure.Audit;
using Pactogg.Api.Infrastructure.Moderation;
using Pactogg.Api.Infrastructure.Options;
using Pactogg.Api.Infrastructure.Redis;
using Pactogg.Api.Infrastructure.Storage;
using Pactogg.Api.Modules.Contracts.Abstractions;
using Pactogg.Api.Modules.Contracts.Domain;
using Pactogg.Api.Modules.Evidence.Abstractions;
using EvidenceEntity = Pactogg.Api.Modules.Evidence.Domain.Evidence;
using ModerationRecordEntity = Pactogg.Api.Modules.Evidence.Domain.ModerationRecord;
using Pactogg.Api.Modules.Evidence.Dtos;
using System.Security.Cryptography;
using Pactogg.Api.Modules.Groups;
using Pactogg.Api.Modules.Groups.Abstractions;
using Pactogg.Api.Modules.Notifications;

namespace Pactogg.Api.Modules.Evidence;

public sealed class EvidenceService
{
    private static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg",
        "image/png",
        "image/webp"
    };

    // CLARIFICACION #30: la primera evidencia pasa el contrato a voting.
    // #59/#60: hasta 3 evidencias y se pueden agregar mientras no haya votos.
    private static readonly HashSet<string> ValidStates = new(StringComparer.OrdinalIgnoreCase)
    {
        "open",
        "accepted",
        "in_progress",
        "evidence_pending",
        "voting"
    };

    private readonly IContractRepository _contracts;
    private readonly IEvidenceRepository _evidence;
    private readonly IGroupMemberRepository _members;
    private readonly IModerationService _moderation;
    private readonly IObjectStorage _storage;
    private readonly IAuditService _audit;
    private readonly EvidenceOptions _options;
    private readonly ICacheService _cache;
    private readonly IGroupRepository _groups;
    private readonly GroupAccess _access;
    private readonly NotificationService _notifications;

    private readonly ContractLifecycleService _lifecycle;
    private readonly IUserRepository _users;
    private readonly AuthOptions _authOptions;

    public EvidenceService(
        IContractRepository contracts,
        IEvidenceRepository evidence,
        IGroupMemberRepository members,
        IModerationService moderation,
        IObjectStorage storage,
        IAuditService audit,
        IOptions<EvidenceOptions> options,
        ICacheService cache,
        IGroupRepository groups,
        GroupAccess access,
        NotificationService notifications,
        ContractLifecycleService lifecycle,
        IUserRepository users,
        IOptions<AuthOptions> authOptions)
    {
        _users = users;
        _authOptions = authOptions.Value;
        _lifecycle = lifecycle;
        _cache = cache;
        _groups = groups;
        _access = access;
        _notifications = notifications;
        _contracts = contracts;
        _evidence = evidence;
        _members = members;
        _moderation = moderation;
        _storage = storage;
        _audit = audit;
        _options = options.Value;
    }

    public async Task<EvidenceResponse> AddLinkAsync(
        ObjectId userId,
        string contractId,
        CreateLinkEvidenceRequest request,
        CancellationToken cancellationToken)
    {
        var contract = await GetContractAsync(userId, contractId, cancellationToken);
        var uri = await ValidateLinkAsync(request, cancellationToken);

        await EnsureEvidenceSlotAsync(contract, cancellationToken);
        var now = DateTime.UtcNow;
        var evidence = new EvidenceEntity
        {
            Id = ObjectId.GenerateNewId(),
            ContractId = contract.Id,
            UserId = userId,
            Type = "link",
            Url = uri.AbsoluteUri,
            Provider = ProviderFor(uri.Host),
            Moderation = new ModerationRecordEntity
            {
                Status = "approved",
                Labels = [],
                ModeratedAt = now
            },
            CreatedAt = now,
            ExpiresAt = null
        };

        await ReserveAndCreateAsync(contract, evidence, cancellationToken);
        await _audit.AppendAsync("evidence.created", userId, "evidence", evidence.Id, contract.GroupId, new { contract_id = contract.Id.ToString(), type = "link" }, cancellationToken);
        await _cache.BumpVersionAsync(CacheExtensions.GroupScope(contract.GroupId), cancellationToken);
        return ToResponse(evidence);
    }

    public async Task<EvidenceResponse> AddPhotoAsync(
        ObjectId userId,
        string contractId,
        byte[] content,
        string contentType,
        CancellationToken cancellationToken)
    {
        if (!AllowedContentTypes.Contains(contentType))
        {
            throw ApiException.Unprocessable(ErrorCodes.UnsupportedMediaType, "Solo se admiten JPEG, PNG o WebP.");
        }

        var contract = await GetContractAsync(userId, contractId, cancellationToken);
        var normalized = await ValidatePhotoAsync(content, contentType, cancellationToken);

        await EnsureEvidenceSlotAsync(contract, cancellationToken);
        var now = DateTime.UtcNow;
        var evidence = new EvidenceEntity
        {
            Id = ObjectId.GenerateNewId(),
            ContractId = contract.Id,
            UserId = userId,
            Type = "photo",
            Moderation = new ModerationRecordEntity
            {
                Status = "approved",
                Labels = [],
                ModeratedAt = now
            },
            CreatedAt = now,
            ExpiresAt = now.AddDays(_options.RetentionDays)
        };

        var reserved = await _contracts.TryAttachEvidenceAsync(contract.Id, userId, now, _options.MaxPerContract, cancellationToken);
        if (reserved is null)
        {
            throw ApiException.Conflict(ErrorCodes.EvidenceLimitReached, "El contrato alcanzó el máximo de evidencias.");
        }

        var objectKey = $"evidence/{contract.GroupId}/{contract.Id}/{evidence.Id}.jpg";
        try
        {
            evidence.ObjectKey = await _storage.PutAsync(normalized, "image/jpeg", objectKey, cancellationToken);
            evidence.SizeBytes = normalized.LongLength;
            await _evidence.CreateAsync(evidence, cancellationToken);
            if (reserved.Status != "voting")
            {
                await StartVotingAsync(reserved, evidence.UserId, cancellationToken);
            }
        }
        catch
        {
            await _contracts.DecrementEvidenceAsync(contract.Id, CancellationToken.None);
            throw;
        }

        await _audit.AppendAsync("evidence.created", userId, "evidence", evidence.Id, contract.GroupId, new { contract_id = contract.Id.ToString(), type = "photo" }, cancellationToken);
        await _cache.BumpVersionAsync(CacheExtensions.GroupScope(contract.GroupId), cancellationToken);
        return ToResponse(evidence);
    }

    /// <summary>
    /// EVI-04 (007-R7): el autor reemplaza su evidencia con las mismas reglas de EVI-02 mientras el reto no tenga votos.
    /// La foto anterior se borra de S3 después de guardar la nueva.
    /// </summary>
    public async Task<EvidenceResponse> ReplaceAsync(
        ObjectId userId,
        string contractId,
        string evidenceId,
        EvidenceUpload upload,
        CancellationToken cancellationToken)
    {
        var (contract, current) = await LoadOwnEditableAsync(userId, evidenceId, contractId, cancellationToken);
        var now = DateTime.UtcNow;
        EvidenceEntity? before;
        string? newKey = null;
        if (upload.Link is not null)
        {
            var uri = await ValidateLinkAsync(upload.Link, cancellationToken);
            before = await _evidence.ReplaceContentAsync(current.Id, "link", uri.AbsoluteUri, ProviderFor(uri.Host), null, null, null, now, cancellationToken);
        }
        else
        {
            var normalized = await ValidatePhotoAsync(upload.Content!, upload.ContentType!, cancellationToken);
            newKey = await _storage.PutAsync(normalized, "image/jpeg", $"evidence/{contract.GroupId}/{contract.Id}/{current.Id}-{ObjectId.GenerateNewId()}.jpg", cancellationToken);
            before = await _evidence.ReplaceContentAsync(current.Id, "photo", null, null, newKey, normalized.LongLength, now.AddDays(_options.RetentionDays), now, cancellationToken);
        }

        if (before is null)
        {
            if (newKey is not null)
            {
                await _storage.DeleteAsync(newKey, CancellationToken.None);
            }

            throw ApiException.NotFound("La evidencia no existe.");
        }

        if (!string.IsNullOrWhiteSpace(before.ObjectKey) && before.ObjectKey != newKey)
        {
            await _storage.DeleteAsync(before.ObjectKey, cancellationToken);
        }

        await _audit.AppendAsync("evidence.replaced", userId, "evidence", current.Id, contract.GroupId, new { contract_id = contract.Id.ToString(), type = upload.Link is null ? "photo" : "link" }, cancellationToken);
        await _cache.BumpVersionAsync(CacheExtensions.GroupScope(contract.GroupId), cancellationToken);
        var updated = await _evidence.GetByIdAsync(current.Id, cancellationToken) ?? throw ApiException.NotFound("La evidencia no existe.");
        return await ToResponseWithUrlAsync(updated, cancellationToken);
    }

    /// <summary>
    /// EVI-05 (007-R8): el autor borra su evidencia antes del primer voto. Si era la única, el reto vuelve a
    /// in_progress y se liberan los jurados (007-D2). Después del primer voto solo el administrador puede quitarla (REP-04).
    /// </summary>
    public async Task<string> DeleteAsync(ObjectId userId, string evidenceId, CancellationToken cancellationToken)
    {
        var (contract, current) = await LoadOwnEditableAsync(userId, evidenceId, null, cancellationToken);
        var deleted = await _evidence.DeleteAsync(current.Id, cancellationToken)
            ?? throw ApiException.NotFound("La evidencia no existe.");
        if (!string.IsNullOrWhiteSpace(deleted.ObjectKey))
        {
            await _storage.DeleteAsync(deleted.ObjectKey, cancellationToken);
        }

        await _contracts.DecrementEvidenceAsync(contract.Id, cancellationToken);
        await _audit.AppendAsync("evidence.deleted", userId, "evidence", current.Id, contract.GroupId, new { contract_id = contract.Id.ToString() }, cancellationToken);

        var status = contract.Status;
        if (await _evidence.CountByContractAsync(contract.Id, cancellationToken) == 0)
        {
            var reverted = await _contracts.TryRevertToInProgressAsync(contract.Id, DateTime.UtcNow, cancellationToken);
            if (reverted is not null)
            {
                status = reverted.Status;
                await _audit.AppendAsync("contract.voting_reverted", userId, "contract", contract.Id, contract.GroupId, new { contract_id = contract.Id.ToString() }, cancellationToken);
            }
        }

        await _cache.BumpVersionAsync(CacheExtensions.GroupScope(contract.GroupId), cancellationToken);
        return status;
    }

    /// <summary>
    /// EVI-06 (007-R9): cualquier miembro guarda en el Muro de la Fama una foto aprobada de un reto cumplido.
    /// La foto se copia a fame/ (fuera de la regla de 30 días del bucket) y pierde su fecha de borrado. Idempotente.
    /// </summary>
    public async Task<EvidenceResponse> SaveInFameAsync(ObjectId userId, string evidenceId, CancellationToken cancellationToken)
    {
        var current = await LoadApprovedAsync(evidenceId, cancellationToken);
        var contract = await _contracts.GetByIdAsync(current.ContractId, cancellationToken)
            ?? throw ApiException.NotFound("La evidencia no existe.");
        await _access.RequireMemberAsync(userId, contract.GroupId, cancellationToken);

        if (contract.Status != "fulfilled")
        {
            throw ApiException.Conflict(ErrorCodes.InvalidContractState, "Solo se guardan en el Muro de la Fama las fotos de retos cumplidos.");
        }

        if (current.Type != "photo" || string.IsNullOrWhiteSpace(current.ObjectKey))
        {
            throw ApiException.Unprocessable(ErrorCodes.ValidationError, "Solo las fotos se guardan en el Muro de la Fama; los enlaces no se borran.");
        }

        if (current.SavedInFame)
        {
            return await ToResponseWithUrlAsync(current, cancellationToken);
        }

        var oldKey = current.ObjectKey;
        var fameKey = oldKey.StartsWith("evidence/", StringComparison.Ordinal) ? "fame/" + oldKey["evidence/".Length..] : "fame/" + oldKey;
        await _storage.CopyAsync(oldKey, fameKey, cancellationToken);
        var saved = await _evidence.SaveInFameAsync(current.Id, fameKey, cancellationToken)
            ?? throw ApiException.NotFound("La evidencia no existe.");
        if (oldKey != fameKey)
        {
            await _storage.DeleteAsync(oldKey, cancellationToken);
        }

        await _audit.AppendAsync("evidence.saved_in_fame", userId, "evidence", current.Id, contract.GroupId, new { contract_id = contract.Id.ToString() }, cancellationToken);
        await _cache.BumpVersionAsync(CacheExtensions.GroupScope(contract.GroupId), cancellationToken);
        return await ToResponseWithUrlAsync(saved, cancellationToken);
    }

    private async Task<EvidenceEntity> LoadApprovedAsync(string evidenceId, CancellationToken cancellationToken)
    {
        if (!ObjectId.TryParse(evidenceId, out var id))
        {
            throw ApiException.NotFound("La evidencia no existe.");
        }

        var evidence = await _evidence.GetByIdAsync(id, cancellationToken);
        if (evidence is null || evidence.Moderation.Status != "approved")
        {
            throw ApiException.NotFound("La evidencia no existe.");
        }

        return evidence;
    }

    /// <summary>Evidencia propia de un reto en voting sin votos ni fase del administrador (EVI-04/05).</summary>
    private async Task<(Contract Contract, EvidenceEntity Evidence)> LoadOwnEditableAsync(ObjectId userId, string evidenceId, string? contractId, CancellationToken cancellationToken)
    {
        var evidence = await LoadApprovedAsync(evidenceId, cancellationToken);
        if (contractId is not null && (!ObjectId.TryParse(contractId, out var cid) || cid != evidence.ContractId))
        {
            throw ApiException.NotFound("La evidencia no existe.");
        }

        var contract = await _contracts.GetByIdAsync(evidence.ContractId, cancellationToken)
            ?? throw ApiException.NotFound("La evidencia no existe.");
        await _access.RequireMemberAsync(userId, contract.GroupId, cancellationToken);

        if (evidence.UserId != userId)
        {
            throw ApiException.Forbidden(ErrorCodes.NotEvidenceOwner, "Solo quien subió la evidencia puede cambiarla o borrarla; el administrador usa los reportes.");
        }

        if (contract.Status != "voting")
        {
            throw ApiException.Conflict(ErrorCodes.InvalidContractState, "El reto ya terminó; la evidencia no se puede cambiar.");
        }

        if (contract.Voters.Count > 0 || contract.AdminDeadlineAt is not null)
        {
            throw ApiException.Conflict(ErrorCodes.VotingStarted, "Ya hay votos: la evidencia quedó congelada.");
        }

        return (contract, evidence);
    }

    private async Task<Uri> ValidateLinkAsync(CreateLinkEvidenceRequest request, CancellationToken cancellationToken)
    {
        if (!string.Equals(request.Type, "link", StringComparison.OrdinalIgnoreCase))
        {
            throw ApiException.Unprocessable(ErrorCodes.ValidationError, "type debe ser link.");
        }

        var uri = EvidenceUrlValidator.Validate(request.Url, _options.AllowedHosts);
        var moderation = await _moderation.ModerateTextAsync(uri.AbsoluteUri, cancellationToken);
        if (!moderation.Approved)
        {
            throw ApiException.Unprocessable(ErrorCodes.ContentRejected, "El enlace fue rechazado por moderación.", new { labels = moderation.Labels });
        }

        return uri;
    }

    private async Task<byte[]> ValidatePhotoAsync(byte[] content, string contentType, CancellationToken cancellationToken)
    {
        if (!AllowedContentTypes.Contains(contentType))
        {
            throw ApiException.Unprocessable(ErrorCodes.UnsupportedMediaType, "Solo se admiten JPEG, PNG o WebP.");
        }

        if (content.LongLength > _options.MaxImageBytes)
        {
            throw ApiException.Unprocessable(ErrorCodes.ImageTooLarge, "La imagen supera el tamaño permitido.");
        }

        var normalized = ImageProcessor.Normalize(content);
        var moderation = await _moderation.ModerateImageAsync(normalized, cancellationToken);
        if (!moderation.Approved)
        {
            throw ApiException.Unprocessable(ErrorCodes.ContentRejected, "La imagen fue rechazada por moderación.", new { labels = moderation.Labels });
        }

        return normalized;
    }

    private async Task<EvidenceResponse> ToResponseWithUrlAsync(EvidenceEntity item, CancellationToken cancellationToken)
    {
        var url = item.Type == "photo" && !string.IsNullOrWhiteSpace(item.ObjectKey)
            ? await _storage.GetReadUrlAsync(item.ObjectKey, TimeSpan.FromMinutes(15), cancellationToken)
            : item.Url;
        return ToResponse(item, url);
    }

    /// <summary>EVI-03: evidencias del reto con URL del CDN; solo miembros del grupo.</summary>
    public async Task<IReadOnlyList<EvidenceResponse>> ListAsync(ObjectId userId, string contractId, CancellationToken cancellationToken)
    {
        if (!ObjectId.TryParse(contractId, out var id))
        {
            throw ApiException.NotFound("El contrato no existe.");
        }

        var contract = await _contracts.GetByIdAsync(id, cancellationToken)
            ?? throw ApiException.NotFound("El contrato no existe.");
        await _access.RequireMemberAsync(userId, contract.GroupId, cancellationToken);

        var items = await _evidence.ListByContractIdsAsync([contract.Id], cancellationToken);
        var responses = new List<EvidenceResponse>(items.Count);
        foreach (var item in items.Where(x => x.Moderation.Status == "approved"))
        {
            responses.Add(await ToResponseWithUrlAsync(item, cancellationToken));
        }

        return responses;
    }

    /// <summary>
    /// Primera evidencia: el reto pasa a voting con jurados al azar (ni el retador ni quien lo cumple),
    /// tantos como indique el grupo (por defecto 3) y 48 h para votar. Avisa a los jurados.
    /// </summary>
    private async Task StartVotingAsync(Contract contract, ObjectId uploaderId, CancellationToken cancellationToken)
    {
        var group = await _groups.GetByIdAsync(contract.GroupId, cancellationToken);
        var wanted = Math.Max(1, group?.Settings.Jurors ?? 3);
        var executor = contract.AcceptedBy ?? contract.TargetId ?? uploaderId;
        var (members, _) = await _members.ListByGroupAsync(contract.GroupId, null, 50, null, cancellationToken);
        var eligible = members
            .Select(x => x.UserId)
            .Where(x => x != contract.ChallengerId && x != executor)
            .ToArray();
        if (_authOptions.RequireVerifiedEmail && eligible.Length > 0)
        {
            // CLARIFICACION #223: solo miembros con correo verificado pueden ser jurado.
            var verified = (await _users.ListByIdsAsync(eligible, cancellationToken))
                .Where(x => x.EmailVerified && x.Status == "active")
                .Select(x => x.Id)
                .ToHashSet();
            eligible = eligible.Where(verified.Contains).ToArray();
        }

        RandomNumberGenerator.Shuffle(eligible.AsSpan());
        var jurors = eligible.Take(wanted).ToList();

        var now = DateTime.UtcNow;
        await _contracts.MarkVotingAsync(contract.Id, executor, jurors, now.AddHours(_lifecycle.Options.VotingHours), cancellationToken);
        if (jurors.Count == 0)
        {
            // Sin jurados elegibles (CLAR #75): decide directamente un administrador que no sea parte.
            var voting = await _contracts.GetByIdAsync(contract.Id, cancellationToken);
            if (voting is not null)
            {
                await _lifecycle.OpenAdminPhaseAsync(voting, now, cancellationToken);
            }

            return;
        }

        await _notifications.NotifyAsync(
            jurors,
            "vote_requested",
            "Te toca ser jurado",
            $"{contract.Game}: {contract.Condition}. ¿Legítimo o trampa?",
            contract.GroupId,
            contract.Id,
            cancellationToken: cancellationToken);
    }

    private async Task<Contract> GetContractAsync(ObjectId userId, string contractId, CancellationToken cancellationToken)
    {
        if (!ObjectId.TryParse(contractId, out var id))
        {
            throw ApiException.NotFound("El contrato no existe.");
        }

        var contract = await _contracts.GetByIdAsync(id, cancellationToken)
            ?? throw ApiException.NotFound("El contrato no existe.");

        if (!ValidStates.Contains(contract.Status))
        {
            throw ApiException.Conflict(ErrorCodes.InvalidContractState, "El contrato no admite evidencias en su estado actual.");
        }

        if (contract.Deadline <= DateTime.UtcNow && contract.Status is "open" or "in_progress")
        {
            throw ApiException.Conflict(ErrorCodes.InvalidContractState, "La fecha límite del reto ya venció.");
        }

        if (contract.Status == "voting" && (contract.Voters.Count > 0 || contract.AdminDeadlineAt is not null))
        {
            throw ApiException.Conflict(ErrorCodes.InvalidContractState, "Ya hay votos: el reto quedó congelado y no admite más evidencias.");
        }

        if (string.Equals(contract.TargetType, "member", StringComparison.OrdinalIgnoreCase))
        {
            if (contract.TargetId != userId)
            {
                throw ApiException.Forbidden(ErrorCodes.NotContractTarget, "No eres el usuario retado.");
            }
        }
        else if (await _members.GetAsync(contract.GroupId, userId, cancellationToken) is null)
        {
            throw ApiException.Forbidden(ErrorCodes.NotContractTarget, "No perteneces al grupo del contrato.");
        }

        return contract;
    }

    private async Task EnsureEvidenceSlotAsync(Contract contract, CancellationToken cancellationToken)
    {
        var count = await _evidence.CountByContractAsync(contract.Id, cancellationToken);
        if (count >= _options.MaxPerContract)
        {
            throw ApiException.Conflict(ErrorCodes.EvidenceLimitReached, "El contrato alcanzó el máximo de evidencias.");
        }
    }

    private async Task ReserveAndCreateAsync(Contract contract, EvidenceEntity evidence, CancellationToken cancellationToken)
    {
        var reserved = await _contracts.TryAttachEvidenceAsync(contract.Id, evidence.UserId, evidence.CreatedAt, _options.MaxPerContract, cancellationToken);
        if (reserved is null)
        {
            throw ApiException.Conflict(ErrorCodes.EvidenceLimitReached, "El contrato alcanzó el máximo de evidencias.");
        }

        try
        {
            await _evidence.CreateAsync(evidence, cancellationToken);
            if (reserved.Status != "voting")
            {
                await StartVotingAsync(reserved, evidence.UserId, cancellationToken);
            }
        }
        catch
        {
            await _contracts.DecrementEvidenceAsync(contract.Id, CancellationToken.None);
            throw;
        }
    }

    private static string ProviderFor(string host)
    {
        var normalized = host.ToLowerInvariant();
        if (normalized.EndsWith("youtube.com", StringComparison.Ordinal) || normalized.EndsWith("youtu.be", StringComparison.Ordinal))
        {
            return "youtube";
        }

        if (normalized.EndsWith("twitch.tv", StringComparison.Ordinal))
        {
            return "twitch";
        }

        if (normalized.EndsWith("medal.tv", StringComparison.Ordinal))
        {
            return "medal";
        }

        if (normalized.EndsWith("streamable.com", StringComparison.Ordinal))
        {
            return "streamable";
        }

        return "other";
    }

    private static EvidenceResponse ToResponse(EvidenceEntity evidence, string? url = null) => new()
    {
        Id = evidence.Id.ToString(),
        ContractId = evidence.ContractId.ToString(),
        UserId = evidence.UserId.ToString(),
        Type = evidence.Type,
        Url = url ?? evidence.Url,
        Provider = evidence.Provider,
        ObjectKey = evidence.ObjectKey,
        Moderation = new ModerationResponse
        {
            Status = evidence.Moderation.Status,
            Labels = evidence.Moderation.Labels,
            ModeratedAt = evidence.Moderation.ModeratedAt
        },
        SavedInFame = evidence.SavedInFame,
        CreatedAt = evidence.CreatedAt,
        ExpiresAt = evidence.ExpiresAt
    };
}
