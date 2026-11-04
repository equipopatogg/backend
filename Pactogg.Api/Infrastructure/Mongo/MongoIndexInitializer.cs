using MongoDB.Driver;

namespace Pactogg.Api.Infrastructure.Mongo;

public sealed class MongoIndexInitializer : BackgroundService
{
    private readonly MongoContext _context;
    private readonly ILogger<MongoIndexInitializer> _logger;

    public MongoIndexInitializer(MongoContext context, ILogger<MongoIndexInitializer> logger)
    {
        _context = context;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await CreateIndexesAsync(stoppingToken);
            _logger.LogInformation("Índices de MongoDB verificados.");
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "No se pudieron crear los índices de MongoDB; la API seguirá arrancando.");
        }
    }

    private async Task CreateIndexesAsync(CancellationToken cancellationToken)
    {
        await _context.Users.Indexes.CreateManyAsync(
        [
            new CreateIndexModel<Modules.Auth.Domain.User>(
                Builders<Modules.Auth.Domain.User>.IndexKeys.Ascending(x => x.Email),
                new CreateIndexOptions { Unique = true, Name = "ux_users_email" }),
            new CreateIndexModel<Modules.Auth.Domain.User>(
                Builders<Modules.Auth.Domain.User>.IndexKeys.Ascending(x => x.NicknameLower),
                new CreateIndexOptions { Unique = true, Name = "ux_users_nickname_lower" })
        ], cancellationToken);

        await _context.RefreshTokens.Indexes.CreateManyAsync(
        [
            new CreateIndexModel<Modules.Auth.Domain.RefreshToken>(
                Builders<Modules.Auth.Domain.RefreshToken>.IndexKeys.Ascending(x => x.TokenHash),
                new CreateIndexOptions { Unique = true, Name = "ux_refresh_token_hash" }),
            new CreateIndexModel<Modules.Auth.Domain.RefreshToken>(
                Builders<Modules.Auth.Domain.RefreshToken>.IndexKeys.Ascending(x => x.UserId).Ascending(x => x.ExpiresAt),
                new CreateIndexOptions { Name = "ix_refresh_user_expiry" })
        ], cancellationToken);

        await _context.Groups.Indexes.CreateOneAsync(
            new CreateIndexModel<Modules.Groups.Domain.Group>(
                Builders<Modules.Groups.Domain.Group>.IndexKeys.Ascending(x => x.InviteCode),
                new CreateIndexOptions { Unique = true, Name = "ux_groups_invite_code" }),
            cancellationToken: cancellationToken);

        await _context.GroupMembers.Indexes.CreateManyAsync(
        [
            new CreateIndexModel<Modules.Groups.Domain.GroupMember>(
                Builders<Modules.Groups.Domain.GroupMember>.IndexKeys.Ascending(x => x.GroupId).Ascending(x => x.UserId),
                new CreateIndexOptions { Unique = true, Name = "ux_group_members_group_user" }),
            new CreateIndexModel<Modules.Groups.Domain.GroupMember>(
                Builders<Modules.Groups.Domain.GroupMember>.IndexKeys.Ascending(x => x.UserId).Ascending(x => x.Status),
                new CreateIndexOptions { Name = "ix_group_members_user_status" }),
            new CreateIndexModel<Modules.Groups.Domain.GroupMember>(
                Builders<Modules.Groups.Domain.GroupMember>.IndexKeys.Ascending(x => x.GroupId).Ascending(x => x.NickLower),
                new CreateIndexOptions { Name = "ix_group_members_group_nick" })
        ], cancellationToken);

        await _context.PointsLedger.Indexes.CreateManyAsync(
        [
            new CreateIndexModel<Modules.Points.Domain.PointsLedgerEntry>(
                Builders<Modules.Points.Domain.PointsLedgerEntry>.IndexKeys
                    .Ascending(x => x.RefId)
                    .Ascending(x => x.Type)
                    .Ascending(x => x.UserId),
                new CreateIndexOptions { Unique = true, Name = "ux_points_ledger_ref_type_user" }),
            new CreateIndexModel<Modules.Points.Domain.PointsLedgerEntry>(
                Builders<Modules.Points.Domain.PointsLedgerEntry>.IndexKeys
                    .Ascending(x => x.GroupId)
                    .Ascending(x => x.UserId)
                    .Descending(x => x.CreatedAt),
                new CreateIndexOptions { Name = "ix_points_ledger_user_group_date" })
        ], cancellationToken);

        await _context.Contracts.Indexes.CreateManyAsync(
        [
            new CreateIndexModel<Modules.Contracts.Domain.Contract>(
                Builders<Modules.Contracts.Domain.Contract>.IndexKeys
                    .Ascending(x => x.GroupId)
                    .Ascending(x => x.Status)
                    .Ascending(x => x.Deadline),
                new CreateIndexOptions { Name = "ix_contracts_group_status_deadline" }),
            new CreateIndexModel<Modules.Contracts.Domain.Contract>(
                Builders<Modules.Contracts.Domain.Contract>.IndexKeys
                    .Ascending(x => x.GroupId)
                    .Descending(x => x.Id),
                new CreateIndexOptions { Name = "ix_contracts_group_created" })
        ], cancellationToken);

        await _context.Evidences.Indexes.CreateOneAsync(
            new CreateIndexModel<Modules.Evidence.Domain.Evidence>(
                Builders<Modules.Evidence.Domain.Evidence>.IndexKeys
                    .Ascending(x => x.ContractId)
                    .Ascending(x => x.CreatedAt),
                new CreateIndexOptions { Name = "ix_evidence_contract_created" }),
            cancellationToken: cancellationToken);

        await _context.Tournaments.Indexes.CreateOneAsync(
            new CreateIndexModel<Modules.Tournaments.Domain.Tournament>(
                Builders<Modules.Tournaments.Domain.Tournament>.IndexKeys
                    .Ascending(x => x.GroupId)
                    .Ascending(x => x.Status)
                    .Ascending(x => x.ScheduledAt),
                new CreateIndexOptions { Name = "ix_tournaments_group_status_scheduled" }),
            cancellationToken: cancellationToken);
    }
}
