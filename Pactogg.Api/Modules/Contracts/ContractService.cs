using MongoDB.Bson;
using Pactogg.Api.Common;
using Pactogg.Api.Infrastructure.Audit;
using Pactogg.Api.Infrastructure.Moderation;
using Pactogg.Api.Modules.Contracts.Abstractions;
using Pactogg.Api.Modules.Contracts.Domain;
using Pactogg.Api.Modules.Contracts.Dtos;
using Pactogg.Api.Modules.Groups.Abstractions;

namespace Pactogg.Api.Modules.Contracts;

public sealed class ContractService
{
    private readonly IContractRepository _contracts;
    private readonly IGroupRepository _groups;
    private readonly IGroupMemberRepository _members;
    private readonly IModerationService _moderation;
    private readonly IAuditService _audit;

    public ContractService(
        IContractRepository contracts,
        IGroupRepository groups,
        IGroupMemberRepository members,
        IModerationService moderation,
        IAuditService audit)
    {
        _contracts = contracts;
        _groups = groups;
        _members = members;
        _moderation = moderation;
        _audit = audit;
    }

    public async Task<ContractResponse> CreateAsync(ObjectId userId, CreateContractRequest request, CancellationToken cancellationToken)
    {
        if (!ObjectId.TryParse(request.GroupId, out var groupId))
        {
            throw ApiException.Unprocessable(ErrorCodes.ValidationError, "group_id no es válido.");
        }

        var group = await _groups.GetByIdAsync(groupId, cancellationToken)
            ?? throw ApiException.NotFound("El grupo no existe.");

        if (await _members.GetAsync(groupId, userId, cancellationToken) is null)
        {
            throw ApiException.Forbidden(ErrorCodes.NotGroupMember, "No perteneces a este grupo.");
        }

        if (!string.Equals(request.Source, "manual", StringComparison.OrdinalIgnoreCase))
        {
            throw ApiException.Unprocessable(ErrorCodes.ValidationError, "En esta entrega solo se admite source=manual.");
        }

        var deadline = request.Deadline.UtcDateTime;
        if (deadline <= DateTime.UtcNow)
        {
            throw ApiException.Unprocessable(ErrorCodes.ValidationError, "El deadline debe ser futuro.");
        }

        if (!group.Settings.BondOptions.Contains(request.BondRequired))
        {
            throw ApiException.Unprocessable(ErrorCodes.InvalidBond, "La fianza no está permitida por el grupo.");
        }

        var targetType = string.IsNullOrWhiteSpace(request.TargetType) ? "group" : request.TargetType.Trim().ToLowerInvariant();
        ObjectId? targetId = null;
        if (targetType == "member")
        {
            if (!ObjectId.TryParse(request.TargetId, out var parsedTarget)
                || await _members.GetAsync(groupId, parsedTarget, cancellationToken) is null)
            {
                throw ApiException.Unprocessable(ErrorCodes.InvalidTarget, "El miembro retado no pertenece al grupo.");
            }

            if (parsedTarget == userId)
            {
                throw ApiException.Unprocessable(ErrorCodes.InvalidTarget, "No puedes retarte a ti mismo.");
            }

            targetId = parsedTarget;
        }
        else if (targetType != "group")
        {
            throw ApiException.Unprocessable(ErrorCodes.InvalidTarget, "target_type debe ser group o member.");
        }

        var reward = ContractRules.NormalizeReward(request.Reward);
        var conditionModeration = await _moderation.ModerateTextAsync(request.Condition, cancellationToken);
        var rewardModeration = await _moderation.ModerateTextAsync(reward, cancellationToken);
        if (!conditionModeration.Approved || !rewardModeration.Approved)
        {
            var labels = conditionModeration.Labels.Concat(rewardModeration.Labels).Distinct().ToArray();
            throw ApiException.Unprocessable(ErrorCodes.ContentRejected, "El contrato fue rechazado por moderación.", new { labels });
        }

        var now = DateTime.UtcNow;
        var contract = new Contract
        {
            Id = ObjectId.GenerateNewId(),
            GroupId = groupId,
            ChallengerId = userId,
            TargetType = targetType,
            TargetId = targetId,
            Game = request.Game.Trim(),
            Condition = request.Condition.Trim(),
            Reward = reward,
            Deadline = deadline,
            BondRequired = request.BondRequired,
            Status = "open",
            Source = "manual",
            EvidenceCount = 0,
            Version = 1,
            CreatedAt = now,
            UpdatedAt = now
        };

        await _contracts.CreateAsync(contract, cancellationToken);
        await _audit.AppendAsync("contract.created", userId, "contract", contract.Id, groupId, new { contract_id = contract.Id.ToString() }, cancellationToken);

        return new ContractResponse
        {
            Id = contract.Id.ToString(),
            GroupId = contract.GroupId.ToString(),
            ChallengerId = contract.ChallengerId.ToString(),
            TargetType = contract.TargetType,
            TargetId = contract.TargetId?.ToString(),
            Game = contract.Game,
            Condition = contract.Condition,
            Reward = contract.Reward,
            Deadline = contract.Deadline,
            BondRequired = contract.BondRequired,
            Status = contract.Status,
            Version = contract.Version,
            CreatedAt = contract.CreatedAt
        };
    }
}
