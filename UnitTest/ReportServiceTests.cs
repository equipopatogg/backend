using FluentAssertions;
using IntegrationTest.Fakes;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using Pactogg.Api.Common;
using Pactogg.Api.Infrastructure.Options;
using Pactogg.Api.Infrastructure.Redis;
using Pactogg.Api.Modules.Auth.Domain;
using Pactogg.Api.Modules.Groups;
using Pactogg.Api.Modules.Reports;

namespace UnitTest;

/// <summary>REP-05/06: bloquear y desbloquear usuarios con repositorios en memoria.</summary>
public sealed class ReportServiceTests
{
    private readonly InMemoryUserRepository _users = new();
    private readonly InMemoryBlockRepository _blocks = new();
    private readonly ReportService _service;
    private readonly ObjectId _me = ObjectId.GenerateNewId();

    public ReportServiceTests()
    {
        var members = new InMemoryGroupMemberRepository();
        var contracts = new InMemoryContractRepository(members, _users, new InMemoryCommitmentRepository(), new InMemoryPointsLedgerRepository());
        var redis = new RedisConnection(Options.Create(new RedisOptions()), NullLogger<RedisConnection>.Instance);
        _service = new ReportService(new InMemoryReportRepository(), _blocks, new InMemoryEvidenceRepository(), new InMemoryVoteRepository(),
            contracts, _users, new FakeObjectStorage(), new GroupAccess(new InMemoryGroupRepository(), members), new FakeAuditService(),
            new RedisCacheService(redis));
    }

    private async Task<ObjectId> UserAsync(string nickname)
    {
        var id = ObjectId.GenerateNewId();
        await _users.CreateAsync(new User { Id = id, Email = $"{nickname}@example.com", Nickname = nickname, NicknameLower = nickname.ToLowerInvariant() },
            CancellationToken.None);
        return id;
    }

    [Fact]
    public async Task Block_UnknownOrMalformedUserIsNotFound()
    {
        var unknown = () => _service.BlockAsync(_me, ObjectId.GenerateNewId().ToString(), CancellationToken.None);
        (await unknown.Should().ThrowAsync<ApiException>()).Which.StatusCode.Should().Be(404);
        var malformed = () => _service.BlockAsync(_me, "../admin", CancellationToken.None);
        (await malformed.Should().ThrowAsync<ApiException>()).Which.StatusCode.Should().Be(404);
    }

    [Fact]
    public async Task Block_YourselfIsRejected()
    {
        var me = await UserAsync("Yo");
        var self = () => _service.BlockAsync(me, me.ToString(), CancellationToken.None);
        (await self.Should().ThrowAsync<ApiException>()).Which.StatusCode.Should().Be(422);
    }

    [Fact]
    public async Task BlockAndUnblock_AreIdempotent()
    {
        var rival = await UserAsync("Rival");

        await _service.BlockAsync(_me, rival.ToString(), CancellationToken.None);
        await _service.BlockAsync(_me, rival.ToString(), CancellationToken.None);
        (await _blocks.IsBlockedAsync(_me, rival, CancellationToken.None)).Should().BeTrue();

        await _service.UnblockAsync(_me, rival.ToString(), CancellationToken.None);
        await _service.UnblockAsync(_me, rival.ToString(), CancellationToken.None);
        (await _blocks.IsBlockedAsync(_me, rival, CancellationToken.None)).Should().BeFalse();

        var malformed = () => _service.UnblockAsync(_me, "no-es-id", CancellationToken.None);
        (await malformed.Should().ThrowAsync<ApiException>()).Which.StatusCode.Should().Be(404);
    }
}
