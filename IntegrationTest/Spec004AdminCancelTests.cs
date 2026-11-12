using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using MongoDB.Bson;
using Xunit;

namespace IntegrationTest;

/// <summary>Spec 004-R16 / 004-AC17 (Q-022): CON-09, un administrador cancela un reto aceptado y la fianza vuelve.</summary>
public sealed class Spec004AdminCancelTests : IClassFixture<PactoggWebFactory>
{
    private readonly PactoggWebFactory _factory;

    public Spec004AdminCancelTests(PactoggWebFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task AC17_AdminCancelsAcceptedContract_BondReturns_OthersCannot()
    {
        var (admin, _) = await Api.RegisterAsync(_factory);
        var (challenger, challengerUser) = await Api.RegisterAsync(_factory);
        var (doer, doerUser) = await Api.RegisterAsync(_factory);
        var (groupId, code) = await Api.CreateGroupAsync(admin);
        await Api.JoinAsync(challenger, code);
        await Api.JoinAsync(doer, code);

        var contractId = await Api.CreateContractAsync(challenger, groupId, doerUser.Id);
        (await doer.PostAsync($"/api/v1/contracts/{contractId}/accept", null)).StatusCode.Should().Be(HttpStatusCode.OK);
        Balance(groupId, doerUser.Id).Should().Be(75);

        var notAdmin = await challenger.PostAsJsonAsync($"/api/v1/contracts/{contractId}/cancel", Reason("Es imposible de cumplir"));
        notAdmin.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await Api.Code(notAdmin)).Should().Be("NOT_GROUP_ADMIN");

        (await admin.PostAsJsonAsync($"/api/v1/contracts/{contractId}/cancel", Reason("no"))).StatusCode
            .Should().Be(HttpStatusCode.UnprocessableEntity, "el motivo lleva de 5 a 140 caracteres");

        var cancelled = await admin.PostAsJsonAsync($"/api/v1/contracts/{contractId}/cancel", Reason("Es imposible de cumplir"));
        cancelled.StatusCode.Should().Be(HttpStatusCode.OK);
        (await Api.Data(cancelled)).GetProperty("status").GetString().Should().Be("cancelled");
        Balance(groupId, doerUser.Id).Should().Be(100, "la fianza vuelve completa");
        _factory.Ledger.Entries.Should().Contain(x => x.RefId == ObjectId.Parse(contractId) && x.Type == "bond_release" && x.Amount == 25);
        var stats = (await _factory.Users.GetByIdAsync(ObjectId.Parse(doerUser.Id), CancellationToken.None))!.Stats;
        stats.WordIndex.Should().Be(50, "cancelar no castiga a nadie");

        (await Api.Data(await doer.GetAsync("/api/v1/notifications"))).EnumerateArray()
            .Should().Contain(x => x.GetProperty("type").GetString() == "contract_cancelled");
        (await Api.Data(await challenger.GetAsync("/api/v1/notifications"))).EnumerateArray()
            .Should().Contain(x => x.GetProperty("type").GetString() == "contract_cancelled");

        (await admin.PostAsJsonAsync($"/api/v1/contracts/{contractId}/cancel", Reason("Otra vez por si acaso"))).StatusCode
            .Should().Be(HttpStatusCode.Conflict, "un reto cancelado ya no cambia");
        var audit = await Api.Data(await admin.GetAsync($"/api/v1/groups/{groupId}/audit"));
        audit.EnumerateArray().Should().Contain(x => x.GetProperty("action").GetString() == "contract.cancelled_by_admin");
        _ = challengerUser;
    }

    [Fact]
    public async Task AC17_AdminWhoAccepted_Cannot_AndVotingCannotBeCancelled()
    {
        var (admin, _) = await Api.RegisterAsync(_factory);
        var (challenger, _) = await Api.RegisterAsync(_factory);
        var (doer, doerUser) = await Api.RegisterAsync(_factory);
        var (juror, _) = await Api.RegisterAsync(_factory);
        var (groupId, code) = await Api.CreateGroupAsync(admin);
        await Api.JoinAsync(challenger, code);
        await Api.JoinAsync(doer, code);
        await Api.JoinAsync(juror, code);

        var forGroup = await Api.CreateContractAsync(challenger, groupId, null);
        (await admin.PostAsync($"/api/v1/contracts/{forGroup}/accept", null)).StatusCode.Should().Be(HttpStatusCode.OK);
        var own = await admin.PostAsJsonAsync($"/api/v1/contracts/{forGroup}/cancel", Reason("Mejor lo cancelo yo"));
        own.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await Api.Code(own)).Should().Be("ADMIN_IS_PARTICIPANT");

        var voting = await Api.CreateContractAsync(challenger, groupId, doerUser.Id);
        (await doer.PostAsync($"/api/v1/contracts/{voting}/accept", null)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await doer.PostAsJsonAsync($"/api/v1/contracts/{voting}/evidence", new Dictionary<string, object?>
        {
            ["type"] = "link",
            ["url"] = "https://www.youtube.com/watch?v=cancel04"
        })).StatusCode.Should().Be(HttpStatusCode.Created);
        var late = await admin.PostAsJsonAsync($"/api/v1/contracts/{voting}/cancel", Reason("Ya no me late este reto"));
        late.StatusCode.Should().Be(HttpStatusCode.Conflict, "en votación lo decide el jurado");

        var open = await Api.CreateContractAsync(challenger, groupId, doerUser.Id);
        (await admin.PostAsJsonAsync($"/api/v1/contracts/{open}/cancel", Reason("Reto imposible de cumplir"))).StatusCode
            .Should().Be(HttpStatusCode.OK, "también sirve antes de que alguien lo acepte");
    }

    private static Dictionary<string, object?> Reason(string reason) => new() { ["reason"] = reason };

    private int Balance(string groupId, string userId) =>
        _factory.Members.Find(ObjectId.Parse(groupId), ObjectId.Parse(userId))!.PointsBalance;
}
