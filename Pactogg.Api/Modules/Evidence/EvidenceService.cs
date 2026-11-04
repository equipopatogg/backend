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
using Pactogg.Api.Modules.Groups.Abstractions;

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
    // TODO(VOT-02): bloquear nuevas evidencias en voting cuando exista el primer voto.
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

    public EvidenceService(
        IContractRepository contracts,
        IEvidenceRepository evidence,
        IGroupMemberRepository members,
        IModerationService moderation,
        IObjectStorage storage,
        IAuditService audit,
        IOptions<EvidenceOptions> options,
        ICacheService cache)
    {
        _cache = cache;
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

        if (content.LongLength > _options.MaxImageBytes)
        {
            throw ApiException.Unprocessable(ErrorCodes.ImageTooLarge, "La imagen supera el tamaño permitido.");
        }

        var contract = await GetContractAsync(userId, contractId, cancellationToken);
        var normalized = ImageProcessor.Normalize(content);
        var moderation = await _moderation.ModerateImageAsync(normalized, cancellationToken);
        if (!moderation.Approved)
        {
            throw ApiException.Unprocessable(ErrorCodes.ContentRejected, "La imagen fue rechazada por moderación.", new { labels = moderation.Labels });
        }

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
                await _contracts.MarkVotingAsync(contract.Id, cancellationToken);
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
                await _contracts.MarkVotingAsync(contract.Id, cancellationToken);
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

    private static EvidenceResponse ToResponse(EvidenceEntity evidence) => new()
    {
        Id = evidence.Id.ToString(),
        ContractId = evidence.ContractId.ToString(),
        UserId = evidence.UserId.ToString(),
        Type = evidence.Type,
        Url = evidence.Url,
        Provider = evidence.Provider,
        ObjectKey = evidence.ObjectKey,
        Moderation = new ModerationResponse
        {
            Status = evidence.Moderation.Status,
            Labels = evidence.Moderation.Labels,
            ModeratedAt = evidence.Moderation.ModeratedAt
        },
        CreatedAt = evidence.CreatedAt,
        ExpiresAt = evidence.ExpiresAt
    };
}
