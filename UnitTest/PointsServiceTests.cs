using FluentAssertions;
using IntegrationTest.Fakes;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using Pactogg.Api.Common;
using Pactogg.Api.Infrastructure.Options;
using Pactogg.Api.Infrastructure.Redis;
using Pactogg.Api.Modules.Groups;
using Pactogg.Api.Modules.Points;

namespace UnitTest;

/// <summary>PTS-01..03 y ACH-03: validaciones de puntos y ranking antes de tocar datos.</summary>
public sealed class PointsServiceTests
{
    private readonly PointsService _service;
    private readonly ObjectId _me = ObjectId.GenerateNewId();
    private readonly string _unknownGroup = ObjectId.GenerateNewId().ToString();

    public PointsServiceTests()
    {
        var members = new InMemoryGroupMemberRepository();
        var redis = new RedisConnection(Options.Create(new RedisOptions()), NullLogger<RedisConnection>.Instance);
        _service = new PointsService(new InMemoryPointsLedgerRepository(), members, new InMemoryUserRepository(),
            new GroupAccess(new InMemoryGroupRepository(), members), new RedisCacheService(redis));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(51)]
    public async Task LimitOutsideOneToFiftyIsRejected(int limit)
    {
        var mine = () => _service.GetMineAsync(_me, _unknownGroup, null, limit, null, CancellationToken.None);
        (await mine.Should().ThrowAsync<ApiException>()).Which.StatusCode.Should().Be(422);
        var ranking = () => _service.GroupRankingAsync(_me, _unknownGroup, limit, CancellationToken.None);
        (await ranking.Should().ThrowAsync<ApiException>()).Which.StatusCode.Should().Be(422);
    }

    [Fact]
    public async Task UnknownMovementTypeIsRejected()
    {
        var mine = () => _service.GetMineAsync(_me, _unknownGroup, "regalo", 20, null, CancellationToken.None);
        (await mine.Should().ThrowAsync<ApiException>()).Which.StatusCode.Should().Be(422);
    }

    [Fact]
    public async Task UnknownGroupOrMovementIsNotFound()
    {
        var ranking = () => _service.GroupRankingAsync(_me, _unknownGroup, 20, CancellationToken.None);
        (await ranking.Should().ThrowAsync<ApiException>()).Which.StatusCode.Should().Be(404);
        var movement = () => _service.GetMovementAsync(_me, "no-es-id", CancellationToken.None);
        (await movement.Should().ThrowAsync<ApiException>()).Which.StatusCode.Should().Be(404);
        var missing = () => _service.GetMovementAsync(_me, ObjectId.GenerateNewId().ToString(), CancellationToken.None);
        (await missing.Should().ThrowAsync<ApiException>()).Which.StatusCode.Should().Be(404);
    }
}
