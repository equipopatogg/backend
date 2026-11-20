using FluentAssertions;
using IntegrationTest.Fakes;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using Pactogg.Api.Common;
using Pactogg.Api.Infrastructure.Options;
using Pactogg.Api.Infrastructure.Push;
using Pactogg.Api.Infrastructure.Redis;
using Pactogg.Api.Modules.Commitments;
using Pactogg.Api.Modules.Fame;
using Pactogg.Api.Modules.Groups;
using Pactogg.Api.Modules.Notifications;
using Pactogg.Api.Modules.Reputation;

namespace UnitTest;

/// <summary>COM-01..05 (Libro de compromisos) y FAM-01 (Muro de la Fama): validaciones antes de leer o cambiar datos.</summary>
public sealed class CommitmentAndFameServiceTests
{
    private readonly CommitmentService _commitments;
    private readonly FameService _fame;
    private readonly ObjectId _me = ObjectId.GenerateNewId();
    private readonly string _unknown = ObjectId.GenerateNewId().ToString();

    public CommitmentAndFameServiceTests()
    {
        var users = new InMemoryUserRepository();
        var members = new InMemoryGroupMemberRepository();
        var commitments = new InMemoryCommitmentRepository();
        var contracts = new InMemoryContractRepository(members, users, commitments, new InMemoryPointsLedgerRepository());
        var access = new GroupAccess(new InMemoryGroupRepository(), members);
        var cache = new RedisCacheService(new RedisConnection(Options.Create(new RedisOptions()), NullLogger<RedisConnection>.Instance));
        var reputation = new ReputationService(users, new InMemoryVoteRepository(), new InMemoryAchievementRepository(), contracts, members, access, cache);
        var notifications = new NotificationService(new InMemoryNotificationRepository(), NullLogger<NotificationService>.Instance, new PushQueue());
        _commitments = new CommitmentService(commitments, users, access, notifications, new FakeAuditService(), reputation);
        _fame = new FameService(contracts, new InMemoryEvidenceRepository(), users, new FakeObjectStorage(), access, cache);
    }

    private static async Task Expect(Func<Task> call, int status) =>
        (await call.Should().ThrowAsync<ApiException>()).Which.StatusCode.Should().Be(status);

    [Fact]
    public async Task CommitmentList_RejectsBadLimitRoleOrStatus()
    {
        await Expect(() => _commitments.ListAsync(_me, _unknown, null, null, 0, null, CancellationToken.None), 422);
        await Expect(() => _commitments.ListAsync(_me, _unknown, "todos", null, 20, null, CancellationToken.None), 422);
        await Expect(() => _commitments.ListAsync(_me, _unknown, "debo", "inventado", 20, null, CancellationToken.None), 422);
    }

    [Fact]
    public async Task CommitmentList_AndSummary_OfAnUnknownGroupAreNotFound()
    {
        await Expect(() => _commitments.ListAsync(_me, _unknown, "me-deben", null, 20, null, CancellationToken.None), 404);
        await Expect(() => _commitments.SummaryAsync(_me, _unknown, CancellationToken.None), 404);
    }

    [Theory]
    [InlineData("no-es-id")]
    [InlineData("000000000000000000000000")]
    public async Task CommitmentDetailCompleteAndRequest_UnknownCommitmentIsNotFound(string id)
    {
        await Expect(() => _commitments.GetAsync(_me, id, CancellationToken.None), 404);
        await Expect(() => _commitments.CompleteAsync(_me, id, CancellationToken.None), 404);
        await Expect(() => _commitments.RequestCompleteAsync(_me, id, CancellationToken.None), 404);
    }

    [Fact]
    public async Task Fame_RejectsBadLimitAndUnknownGroup()
    {
        await Expect(() => _fame.ListAsync(_me, _unknown, 51, null, CancellationToken.None), 422);
        await Expect(() => _fame.ListAsync(_me, _unknown, 20, null, CancellationToken.None), 404);
    }
}

/// <summary>AUD-01: el historial del grupo valida el límite y la pertenencia antes de leer eventos.</summary>
public sealed class AuditQueryServiceTests
{
    private readonly Pactogg.Api.Modules.Audit.AuditQueryService _service;

    public AuditQueryServiceTests()
    {
        var members = new InMemoryGroupMemberRepository();
        _service = new Pactogg.Api.Modules.Audit.AuditQueryService(new FakeAuditService(),
            new GroupAccess(new InMemoryGroupRepository(), members), new InMemoryUserRepository());
    }

    [Fact]
    public async Task BadLimitIsRejectedAndUnknownGroupIsNotFound()
    {
        var me = ObjectId.GenerateNewId();
        var group = ObjectId.GenerateNewId().ToString();
        var badLimit = () => _service.ListAsync(me, group, null, null, 0, null, CancellationToken.None);
        (await badLimit.Should().ThrowAsync<ApiException>()).Which.StatusCode.Should().Be(422);
        var unknown = () => _service.ListAsync(me, group, null, null, 20, null, CancellationToken.None);
        (await unknown.Should().ThrowAsync<ApiException>()).Which.StatusCode.Should().Be(404);
    }
}
