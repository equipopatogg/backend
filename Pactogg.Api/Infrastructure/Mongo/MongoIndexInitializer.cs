using MongoDB.Bson;
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
                new CreateIndexOptions { Unique = true, Name = "ux_users_nickname_lower" }),
            new CreateIndexModel<Modules.Auth.Domain.User>(
                Builders<Modules.Auth.Domain.User>.IndexKeys.Ascending(x => x.GoogleSub),
                new CreateIndexOptions<Modules.Auth.Domain.User>
                {
                    Unique = true,
                    Name = "ux_users_google_sub",
                    PartialFilterExpression = Builders<Modules.Auth.Domain.User>.Filter.Type(x => x.GoogleSub, MongoDB.Bson.BsonType.String)
                })
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
                new CreateIndexOptions { Name = "ix_group_members_group_nick" }),
            new CreateIndexModel<Modules.Groups.Domain.GroupMember>(
                Builders<Modules.Groups.Domain.GroupMember>.IndexKeys.Ascending(x => x.GroupId).Descending(x => x.PointsBalance),
                new CreateIndexOptions { Name = "ix_group_members_group_points" })
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

        // Spec 007-R10: fotos vencidas fuera del Muro de la Fama.
        await _context.Evidences.Indexes.CreateOneAsync(
            new CreateIndexModel<Modules.Evidence.Domain.Evidence>(
                Builders<Modules.Evidence.Domain.Evidence>.IndexKeys.Ascending(x => x.ExpiresAt),
                new CreateIndexOptions<Modules.Evidence.Domain.Evidence>
                {
                    Name = "ix_evidence_expires_photo",
                    PartialFilterExpression = Builders<Modules.Evidence.Domain.Evidence>.Filter.Eq(x => x.Type, "photo")
                }),
            cancellationToken: cancellationToken);

        await _context.Tournaments.Indexes.CreateOneAsync(
            new CreateIndexModel<Modules.Tournaments.Domain.Tournament>(
                Builders<Modules.Tournaments.Domain.Tournament>.IndexKeys
                    .Ascending(x => x.GroupId)
                    .Ascending(x => x.Status)
                    .Ascending(x => x.ScheduledAt),
                new CreateIndexOptions { Name = "ix_tournaments_group_status_scheduled" }),
            cancellationToken: cancellationToken);

        await _context.Votes.Indexes.CreateOneAsync(
            new CreateIndexModel<Modules.Votes.Domain.Vote>(
                Builders<Modules.Votes.Domain.Vote>.IndexKeys.Ascending(x => x.ContractId).Ascending(x => x.JurorId),
                new CreateIndexOptions { Unique = true, Name = "ux_votes_contract_juror" }),
            cancellationToken: cancellationToken);

        await _context.Contracts.Indexes.CreateOneAsync(
            new CreateIndexModel<Modules.Contracts.Domain.Contract>(
                Builders<Modules.Contracts.Domain.Contract>.IndexKeys.Ascending(x => x.Jurors).Ascending(x => x.Status),
                new CreateIndexOptions { Name = "ix_contracts_jurors_status" }),
            cancellationToken: cancellationToken);

        await _context.Commitments.Indexes.CreateManyAsync(
        [
            new CreateIndexModel<Modules.Commitments.Domain.Commitment>(
                Builders<Modules.Commitments.Domain.Commitment>.IndexKeys.Ascending(x => x.ContractId),
                new CreateIndexOptions { Unique = true, Name = "ux_commitments_contract" }),
            new CreateIndexModel<Modules.Commitments.Domain.Commitment>(
                Builders<Modules.Commitments.Domain.Commitment>.IndexKeys
                    .Ascending(x => x.GroupId).Ascending(x => x.DebtorId).Ascending(x => x.Status),
                new CreateIndexOptions { Name = "ix_commitments_group_debtor_status" }),
            new CreateIndexModel<Modules.Commitments.Domain.Commitment>(
                Builders<Modules.Commitments.Domain.Commitment>.IndexKeys
                    .Ascending(x => x.GroupId).Ascending(x => x.CreditorId).Ascending(x => x.Status),
                new CreateIndexOptions { Name = "ix_commitments_group_creditor_status" })
        ], cancellationToken);

        await _context.AuditEvents.Indexes.CreateOneAsync(
            new CreateIndexModel<Infrastructure.Audit.AuditEvent>(
                Builders<Infrastructure.Audit.AuditEvent>.IndexKeys.Ascending(x => x.GroupId).Descending(x => x.Id),
                new CreateIndexOptions { Name = "ix_audit_group_id" }),
            cancellationToken: cancellationToken);

        await _context.Devices.Indexes.CreateManyAsync(
        [
            new CreateIndexModel<Modules.Devices.Device>(
                Builders<Modules.Devices.Device>.IndexKeys.Ascending(x => x.FcmToken),
                new CreateIndexOptions { Name = "ux_devices_fcm_token", Unique = true }),
            new CreateIndexModel<Modules.Devices.Device>(
                Builders<Modules.Devices.Device>.IndexKeys.Ascending(x => x.UserId),
                new CreateIndexOptions { Name = "ix_devices_user" })
        ], cancellationToken);

        // REP-01/05: un reporte por persona y objetivo; un bloqueo por pareja.
        await _context.Reports.Indexes.CreateManyAsync(
        [
            new CreateIndexModel<Modules.Reports.Report>(
                Builders<Modules.Reports.Report>.IndexKeys.Ascending(x => x.ReporterId).Ascending(x => x.TargetType).Ascending(x => x.TargetId),
                new CreateIndexOptions { Name = "ux_reports_reporter_target", Unique = true }),
            new CreateIndexModel<Modules.Reports.Report>(
                Builders<Modules.Reports.Report>.IndexKeys.Ascending(x => x.GroupId).Ascending(x => x.Status).Descending(x => x.Id),
                new CreateIndexOptions { Name = "ix_reports_group_status" })
        ], cancellationToken);
        await _context.Blocks.Indexes.CreateManyAsync(
        [
            new CreateIndexModel<Modules.Reports.UserBlock>(
                Builders<Modules.Reports.UserBlock>.IndexKeys.Ascending(x => x.BlockerId).Ascending(x => x.BlockedId),
                new CreateIndexOptions { Name = "ux_blocks_pair", Unique = true }),
            new CreateIndexModel<Modules.Reports.UserBlock>(
                Builders<Modules.Reports.UserBlock>.IndexKeys.Ascending(x => x.BlockedId),
                new CreateIndexOptions { Name = "ix_blocks_blocked" })
        ], cancellationToken);

        // Spec 008-R10: un logro por persona, código y fuente.
        await _context.Achievements.Indexes.CreateManyAsync(
        [
            new CreateIndexModel<Modules.Reputation.Achievement>(
                Builders<Modules.Reputation.Achievement>.IndexKeys.Ascending(x => x.UserId).Ascending(x => x.Code).Ascending(x => x.SourceKey),
                new CreateIndexOptions { Name = "ux_achievements_user_code_source", Unique = true }),
            new CreateIndexModel<Modules.Reputation.Achievement>(
                Builders<Modules.Reputation.Achievement>.IndexKeys.Ascending(x => x.UserId).Descending(x => x.EarnedAt),
                new CreateIndexOptions { Name = "ix_achievements_user_earned" })
        ], cancellationToken);

        // Spec 009-R4: compromisos pendientes por antigüedad.
        await _context.Commitments.Indexes.CreateOneAsync(
            new CreateIndexModel<Modules.Commitments.Domain.Commitment>(
                Builders<Modules.Commitments.Domain.Commitment>.IndexKeys.Ascending(x => x.Status).Ascending(x => x.CreatedAt),
                new CreateIndexOptions { Name = "ix_commitments_status_created" }),
            cancellationToken: cancellationToken);

        // CLARIFICACION #118: retención de 30 días. El índice TTL anterior (90 d) se reemplaza.
        try
        {
            await _context.Notifications.Indexes.DropOneAsync("ttl_notifications_90d", cancellationToken);
        }
        catch (MongoCommandException)
        {
            // No existía: instalación nueva.
        }

        await _context.Notifications.Indexes.CreateManyAsync(
        [
            new CreateIndexModel<Modules.Notifications.Domain.Notification>(
                Builders<Modules.Notifications.Domain.Notification>.IndexKeys
                    .Ascending(x => x.UserId).Ascending(x => x.ReadAt).Descending(x => x.Id),
                new CreateIndexOptions { Name = "ix_notifications_user_read" }),
            new CreateIndexModel<Modules.Notifications.Domain.Notification>(
                Builders<Modules.Notifications.Domain.Notification>.IndexKeys.Ascending(x => x.CreatedAt),
                new CreateIndexOptions { Name = "ttl_notifications_30d", ExpireAfter = TimeSpan.FromDays(30) }),
            new CreateIndexModel<Modules.Notifications.Domain.Notification>(
                Builders<Modules.Notifications.Domain.Notification>.IndexKeys.Ascending(x => x.DedupeKey),
                new CreateIndexOptions<Modules.Notifications.Domain.Notification>
                {
                    Name = "ux_notifications_dedupe",
                    Unique = true,
                    PartialFilterExpression = Builders<Modules.Notifications.Domain.Notification>.Filter.Type(x => x.DedupeKey, BsonType.String)
                })
        ], cancellationToken);
    }
}
