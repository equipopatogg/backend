using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Bson;
using Pactogg.Api.Modules.Commitments.Domain;
using Pactogg.Api.Modules.Maintenance;
using Xunit;

namespace IntegrationTest;

/// <summary>Spec 006 (AC10, AC11, AC13) y spec 009 (AC2): updated_at, Idempotency-Key opcional y compromiso vencido.</summary>
public sealed class Spec006And009Tests : IClassFixture<PactoggWebFactory>
{
    private readonly PactoggWebFactory _factory;

    public Spec006And009Tests(PactoggWebFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task AC11_CreateContract_WithTheSameIdempotencyKey_CreatesOnlyOne_WithoutKeyWorksAsBefore()
    {
        var (owner, _) = await Api.RegisterAsync(_factory);
        var (groupId, _) = await Api.CreateGroupAsync(owner);
        _factory.PadMembers(groupId, 2);
        var body = new Dictionary<string, object?>
        {
            ["game"] = "Rocket League",
            ["condition"] = "Meter un gol de chilena",
            ["reward"] = "Una pizza grande",
            ["deadline"] = DateTimeOffset.UtcNow.AddDays(3).ToString("O"),
            ["bond_required"] = 25,
            ["target_type"] = "group",
            ["source"] = "manual"
        };

        async Task<HttpResponseMessage> Send(string? key)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/groups/{groupId}/contracts") { Content = JsonContent.Create(body) };
            if (key is null)
            {
                request.Headers.Add(IdempotencyTestHandler.SkipHeader, "1");
            }
            else
            {
                request.Headers.Add("Idempotency-Key", key);
            }

            return await owner.SendAsync(request);
        }

        var key = "borrador-" + Guid.NewGuid().ToString("N");
        var first = await Send(key);
        first.StatusCode.Should().Be(HttpStatusCode.Created);
        var retry = await Send(key);
        retry.StatusCode.Should().Be(HttpStatusCode.Created);
        retry.Headers.GetValues("Idempotent-Replayed").Should().ContainSingle("true");
        (await Api.Data(retry)).GetProperty("id").GetString().Should().Be((await Api.Data(first)).GetProperty("id").GetString());

        var noKey1 = await Send(null);
        var noKey2 = await Send(null);
        noKey1.StatusCode.Should().Be(HttpStatusCode.Created);
        noKey2.StatusCode.Should().Be(HttpStatusCode.Created, "sin cabecera la clave sigue siendo opcional");
        (await Api.Data(noKey1)).GetProperty("id").GetString().Should().NotBe((await Api.Data(noKey2)).GetProperty("id").GetString());
    }

    [Fact]
    public async Task AC10_MemberCommitmentAndProfile_IncludeUpdatedAt()
    {
        var (owner, ownerUser) = await Api.RegisterAsync(_factory);
        var (friend, friendUser) = await Api.RegisterAsync(_factory);
        var (groupId, code) = await Api.CreateGroupAsync(owner);
        await Api.JoinAsync(friend, code);

        var members = await Api.Data(await owner.GetAsync($"/api/v1/groups/{groupId}/members"));
        members.EnumerateArray().All(x => x.TryGetProperty("updated_at", out _)).Should().BeTrue();
        var me = await Api.Data(await owner.GetAsync("/api/v1/users/me"));
        me.TryGetProperty("updated_at", out _).Should().BeTrue();

        var commitment = NewCommitment(groupId, ownerUser.Id, friendUser.Id, DateTime.UtcNow);
        _factory.Commitments.Add(commitment);
        var detail = await Api.Data(await friend.GetAsync($"/api/v1/commitments/{commitment.Id}"));
        detail.TryGetProperty("updated_at", out _).Should().BeTrue();
    }

    [Fact]
    public async Task AC13_CommitmentPendingMoreThan7Days_NotifiesTheDebtorOnce_AndCountsForTheIndex()
    {
        var (owner, ownerUser) = await Api.RegisterAsync(_factory);
        var (friend, friendUser) = await Api.RegisterAsync(_factory);
        var (groupId, code) = await Api.CreateGroupAsync(owner);
        await Api.JoinAsync(friend, code);
        var now = DateTime.UtcNow;
        var late = NewCommitment(groupId, ownerUser.Id, friendUser.Id, now.AddDays(-8));
        var recent = NewCommitment(groupId, ownerUser.Id, friendUser.Id, now.AddDays(-2));
        _factory.Commitments.Add(late);
        _factory.Commitments.Add(recent);

        for (var run = 0; run < 2; run++)
        {
            using var scope = _factory.Services.CreateScope();
            await scope.ServiceProvider.GetRequiredService<MaintenanceService>().FlagOverdueCommitmentsAsync(now, CancellationToken.None);
        }

        late.OverdueAt.Should().NotBeNull();
        recent.OverdueAt.Should().BeNull();
        var notifications = await Api.Data(await owner.GetAsync("/api/v1/notifications"));
        notifications.EnumerateArray().Count(x => x.GetProperty("type").GetString() == "commitment_overdue").Should().Be(1, "un solo aviso aunque la tarea corra dos veces");
        (await Api.Data(await friend.GetAsync("/api/v1/notifications"))).EnumerateArray()
            .Should().NotContain(x => x.GetProperty("type").GetString() == "commitment_overdue", "solo se avisa al deudor");

        var stats = (await _factory.Users.GetByIdAsync(ObjectId.Parse(ownerUser.Id), CancellationToken.None))!.Stats;
        stats.CommitmentsOverdue.Should().Be(1);
        stats.WordIndex.Should().Be(46);

        // El compromiso reciente se cumple a tiempo: +2 al Índice del deudor.
        (await friend.PostAsync($"/api/v1/commitments/{recent.Id}/complete", null)).StatusCode.Should().Be(HttpStatusCode.OK);
        stats.CommitmentsOnTime.Should().Be(1);
        stats.WordIndex.Should().Be(48);

        // El vencido ya no suma "a tiempo" aunque se cumpla después.
        (await friend.PostAsync($"/api/v1/commitments/{late.Id}/complete", null)).StatusCode.Should().Be(HttpStatusCode.OK);
        stats.CommitmentsOnTime.Should().Be(1);
    }

    private static Commitment NewCommitment(string groupId, string debtorId, string creditorId, DateTime createdAt) => new()
    {
        Id = ObjectId.GenerateNewId(),
        GroupId = ObjectId.Parse(groupId),
        ContractId = ObjectId.GenerateNewId(),
        DebtorId = ObjectId.Parse(debtorId),
        CreditorId = ObjectId.Parse(creditorId),
        Description = "Unos tacos",
        Game = "Valorant",
        Status = "pending",
        CreatedAt = createdAt,
        UpdatedAt = createdAt
    };
}
