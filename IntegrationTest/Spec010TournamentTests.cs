using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using MongoDB.Bson;
using Pactogg.Api.Modules.Tournaments;
using Xunit;

namespace IntegrationTest;

/// <summary>Spec 010, parte 1: TOR-01, TOR-03..08 y lecturas MAT-01, MAT-02, MAT-07.</summary>
public sealed class Spec010TournamentTests : IClassFixture<PactoggWebFactory>
{
    private readonly PactoggWebFactory _factory;

    public Spec010TournamentTests(PactoggWebFactory factory)
    {
        _factory = factory;
    }

    // ---------------------------------------------------------------- BracketBuilder (010-AC3)

    [Theory]
    [InlineData(2, 2, 0)]
    [InlineData(3, 4, 1)]
    [InlineData(4, 4, 0)]
    [InlineData(5, 8, 3)]
    [InlineData(8, 8, 0)]
    [InlineData(9, 16, 7)]
    [InlineData(16, 16, 0)]
    public void AC3_Bracket_FillsWithByesThatAdvanceAlone(int groups, int size, int byes)
    {
        var ids = Enumerable.Range(0, groups).Select(_ => ObjectId.GenerateNewId()).ToList();
        var matches = BracketBuilder.Build(ObjectId.GenerateNewId(), ids, new Random(groups), DateTime.UtcNow);

        BracketBuilder.SizeFor(groups).Should().Be(size);
        matches.Should().HaveCount(size - 1, "eliminación directa: una partida menos que lugares");
        var first = matches.Where(x => x.Round == 1).ToList();
        first.Should().HaveCount(size / 2);
        first.Count(x => x.Status == "bye").Should().Be(byes);
        first.Should().OnlyContain(x => x.P1GroupId != null, "nunca hay bye contra bye");
        first.SelectMany(x => new[] { x.P1GroupId, x.P2GroupId }).OfType<ObjectId>().Should().BeEquivalentTo(ids, "cada grupo aparece una sola vez");

        foreach (var bye in first.Where(x => x.Status == "bye"))
        {
            bye.WinnerGroupId.Should().Be(bye.P1GroupId);
            var next = matches.Single(x => x.Id == bye.NextMatchId);
            (bye.Position % 2 == 0 ? next.P1GroupId : next.P2GroupId).Should().Be(bye.P1GroupId, "el bye avanza solo");
        }

        matches.Where(x => x.Round < matches.Max(m => m.Round)).All(x => x.NextMatchId != null).Should().BeTrue();
        matches.Single(x => x.Round == matches.Max(m => m.Round)).NextMatchId.Should().BeNull("la final no tiene siguiente");
    }

    // ---------------------------------------------------------------- TOR-01, TOR-06 (010-AC1)

    [Fact]
    public async Task AC1_CreateCharges50_OtherAdminJoins_PoolIs100_RetryDoesNotChargeTwice_NoBalance402()
    {
        var a = await AdminGroupAsync("Org");
        var b = await AdminGroupAsync("Rivales");

        var created = await a.Client.PostAsJsonAsync($"/api/v1/groups/{a.GroupId}/tournaments", Body(entry: 50, size: 4));
        created.StatusCode.Should().Be(HttpStatusCode.Created);
        var t = await Api.Data(created);
        var id = t.GetProperty("id").GetString()!;
        var code = t.GetProperty("code").GetString()!;
        code.Should().HaveLength(6);
        t.GetProperty("status").GetString().Should().Be("open");
        t.GetProperty("prize_pool").GetInt32().Should().Be(50);
        t.GetProperty("participants").EnumerateArray().Select(x => x.GetString()).Should().Equal(a.GroupId);
        Balance(a.GroupId, a.UserId).Should().Be(50, "el organizador pagó 50 de sus 100 (010-D1)");

        var key = "join-" + Guid.NewGuid().ToString("N");
        var join = await JoinAsync(b.Client, id, b.GroupId, code, key);
        join.StatusCode.Should().Be(HttpStatusCode.OK);
        (await Api.Data(join)).GetProperty("prize_pool").GetInt32().Should().Be(100);
        var retry = await JoinAsync(b.Client, id, b.GroupId, code, key);
        retry.StatusCode.Should().Be(HttpStatusCode.OK);
        Balance(b.GroupId, b.UserId).Should().Be(50, "el reintento con la misma clave no cobra dos veces");
        _factory.Tournaments.Get(ObjectId.Parse(id)).PrizePool.Should().Be(100);
        _factory.Ledger.Entries.Where(x => x.RefId == ObjectId.Parse(id)).Should().OnlyContain(x => x.Type == "tournament_entry").And.HaveCount(2);

        var again = await JoinAsync(b.Client, id, b.GroupId, code, null);
        again.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await Api.Code(again)).Should().Be("ALREADY_IN_TOURNAMENT");

        var poor = await AdminGroupAsync("Sin saldo");
        _factory.Members.Find(ObjectId.Parse(poor.GroupId), ObjectId.Parse(poor.UserId))!.PointsBalance = 10;
        var broke = await JoinAsync(poor.Client, code, poor.GroupId, null, null);
        broke.StatusCode.Should().Be(HttpStatusCode.PaymentRequired, "con el código en la ruta no hace falta repetirlo en el cuerpo");
        _factory.Tournaments.Get(ObjectId.Parse(id)).Participants.Should().HaveCount(2);

        var wrongCode = await JoinAsync(poor.Client, id, poor.GroupId, "ZZZZZZ", null);
        wrongCode.StatusCode.Should().Be(HttpStatusCode.NotFound, "sin el código correcto no se revela el torneo");

        var (notAdmin, _) = await Api.RegisterAsync(_factory);
        await Api.JoinAsync(notAdmin, b.InviteCode);
        (await JoinAsync(notAdmin, code, b.GroupId, null, null)).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var second = await a.Client.PostAsJsonAsync($"/api/v1/groups/{a.GroupId}/tournaments", Body(entry: 0, size: 4));
        second.StatusCode.Should().Be(HttpStatusCode.Conflict, "un torneo activo por grupo organizador");
        (await Api.Code(second)).Should().Be("TOURNAMENT_ALREADY_ACTIVE");
    }

    [Fact]
    public async Task AC1_Validation_SizeEntryDateAndAdminOnly()
    {
        var a = await AdminGroupAsync("Validar");
        (await a.Client.PostAsJsonAsync($"/api/v1/groups/{a.GroupId}/tournaments", Body(entry: 50, size: 6))).StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await a.Client.PostAsJsonAsync($"/api/v1/groups/{a.GroupId}/tournaments", Body(entry: 501, size: 4))).StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await a.Client.PostAsJsonAsync($"/api/v1/groups/{a.GroupId}/tournaments", Body(entry: 10, size: 4, days: -1))).StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);

        var (member, _) = await Api.RegisterAsync(_factory);
        await Api.JoinAsync(member, a.InviteCode);
        var notAdmin = await member.PostAsJsonAsync($"/api/v1/groups/{a.GroupId}/tournaments", Body(entry: 0, size: 4));
        notAdmin.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await Api.Code(notAdmin)).Should().Be("NOT_GROUP_ADMIN");

        _factory.Members.Find(ObjectId.Parse(a.GroupId), ObjectId.Parse(a.UserId))!.PointsBalance = 5;
        (await a.Client.PostAsJsonAsync($"/api/v1/groups/{a.GroupId}/tournaments", Body(entry: 50, size: 4))).StatusCode.Should().Be(HttpStatusCode.PaymentRequired);
    }

    // ---------------------------------------------------------------- TOR-04, TOR-07 (010-AC2)

    [Fact]
    public async Task AC2_EditAndLeaveWhileOpen_AfterStart409_OrganizerGroupCannotLeave()
    {
        var a = await AdminGroupAsync("Edita");
        var b = await AdminGroupAsync("Sale");
        var c = await AdminGroupAsync("Queda");
        var (id, code) = await CreateAsync(a, entry: 30, size: 8);
        (await JoinAsync(b.Client, code, b.GroupId, null, null)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await JoinAsync(c.Client, code, c.GroupId, null, null)).StatusCode.Should().Be(HttpStatusCode.OK);

        var edit = await a.Client.PatchAsJsonAsync($"/api/v1/tournaments/{id}", new Dictionary<string, object?> { ["game"] = "Rocket League", ["max_groups"] = 4 });
        edit.StatusCode.Should().Be(HttpStatusCode.OK);
        (await Api.Data(edit)).GetProperty("max_groups").GetInt32().Should().Be(4);
        (await b.Client.PatchAsJsonAsync($"/api/v1/tournaments/{id}", new Dictionary<string, object?> { ["game"] = "X" })).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        (await b.Client.DeleteAsync($"/api/v1/tournaments/{id}/join?group_id={b.GroupId}")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        Balance(b.GroupId, b.UserId).Should().Be(100, "recupera su inscripción");
        _factory.Tournaments.Get(ObjectId.Parse(id)).PrizePool.Should().Be(60);

        var organizerLeaves = await a.Client.DeleteAsync($"/api/v1/tournaments/{id}/join?group_id={a.GroupId}");
        organizerLeaves.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await Api.Code(organizerLeaves)).Should().Be("ORGANIZER_GROUP_CANNOT_LEAVE");

        (await a.Client.PostAsync($"/api/v1/tournaments/{id}/start", null)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await a.Client.PatchAsJsonAsync($"/api/v1/tournaments/{id}", new Dictionary<string, object?> { ["game"] = "Tarde" })).StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await c.Client.DeleteAsync($"/api/v1/tournaments/{id}/join?group_id={c.GroupId}")).StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    // ---------------------------------------------------------------- TOR-08, MAT-01, MAT-02, MAT-07 (010-AC3, AC8)

    [Fact]
    public async Task AC3_AC8_StartWith3Groups_BracketOf4WithOneBye_NextMatchAndAccess()
    {
        var a = await AdminGroupAsync("Uno");
        var b = await AdminGroupAsync("Dos");
        var c = await AdminGroupAsync("Tres");
        var (id, code) = await CreateAsync(a, entry: 0, size: 4);

        var tooFew = await a.Client.PostAsync($"/api/v1/tournaments/{id}/start", null);
        tooFew.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await Api.Code(tooFew)).Should().Be("NOT_ENOUGH_GROUPS");

        await JoinAsync(b.Client, code, b.GroupId, null, null);
        await JoinAsync(c.Client, code, c.GroupId, null, null);
        (await b.Client.PostAsync($"/api/v1/tournaments/{id}/start", null)).StatusCode.Should().Be(HttpStatusCode.Forbidden, "solo el organizador inicia");

        var started = await a.Client.PostAsync($"/api/v1/tournaments/{id}/start", null);
        started.StatusCode.Should().Be(HttpStatusCode.OK);
        var t = await Api.Data(started);
        t.GetProperty("status").GetString().Should().Be("in_progress");
        t.TryGetProperty("code", out _).Should().BeFalse("después de iniciar ya no se reparte el código");

        var bracket = await Api.Data(await c.Client.GetAsync($"/api/v1/tournaments/{id}/bracket"));
        bracket.GetProperty("size").GetInt32().Should().Be(4);
        bracket.GetProperty("rounds").GetInt32().Should().Be(2);
        var matches = bracket.GetProperty("matches").EnumerateArray().ToList();
        matches.Should().HaveCount(3);
        matches.Count(x => x.GetProperty("status").GetString() == "bye").Should().Be(1);
        var final = matches.Single(x => x.GetProperty("round").GetInt32() == 2);
        (final.TryGetProperty("p1_group_id", out _) || final.TryGetProperty("p2_group_id", out _)).Should().BeTrue("el bye ya está en la final");

        var playable = matches.Single(x => x.GetProperty("round").GetInt32() == 1 && x.GetProperty("status").GetString() == "pending");
        var groupsInPlayable = new[] { playable.GetProperty("p1_group_id").GetString(), playable.GetProperty("p2_group_id").GetString() };
        var player = new[] { a, b, c }.First(x => groupsInPlayable.Contains(x.GroupId));
        var next = await Api.Data(await player.Client.GetAsync("/api/v1/me/matches/next"));
        next.GetProperty("match").GetProperty("id").GetString().Should().Be(playable.GetProperty("id").GetString());
        next.GetProperty("my_group_id").GetString().Should().Be(player.GroupId);
        next.GetProperty("game").GetString().Should().Be("Valorant");

        var byeGroup = new[] { a, b, c }.Single(x => !groupsInPlayable.Contains(x.GroupId));
        var waiting = await byeGroup.Client.GetAsync("/api/v1/me/matches/next");
        waiting.StatusCode.Should().Be(HttpStatusCode.OK);
        (await waiting.Content.ReadAsStringAsync()).Should().Be("{\"data\":null}", "quien pasó por bye espera a su rival de la final");

        var matchId = playable.GetProperty("id").GetString();
        (await player.Client.GetAsync($"/api/v1/matches/{matchId}")).StatusCode.Should().Be(HttpStatusCode.OK);

        var (outsider, _) = await Api.RegisterAsync(_factory);
        (await outsider.GetAsync($"/api/v1/tournaments/{id}")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await outsider.GetAsync($"/api/v1/tournaments/{id}/bracket")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await outsider.GetAsync($"/api/v1/matches/{matchId}")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task AC8_Detail_ShowsGroupsWithoutPersonalData_CodeOnlyForAdmins_PreviewByCode()
    {
        var a = await AdminGroupAsync("Detalle");
        var b = await AdminGroupAsync("Invitado");
        var (id, code) = await CreateAsync(a, entry: 0, size: 4);
        await JoinAsync(b.Client, code, b.GroupId, null, null);

        var (member, _) = await Api.RegisterAsync(_factory);
        await Api.JoinAsync(member, b.InviteCode);
        var forMember = await Api.Data(await member.GetAsync($"/api/v1/tournaments/{id}"));
        forMember.TryGetProperty("code", out _).Should().BeFalse("un miembro que no es administrador no ve el código");
        var groups = forMember.GetProperty("groups").EnumerateArray().ToList();
        groups.Should().HaveCount(2);
        groups.Single(x => x.GetProperty("is_organizer").GetBoolean()).GetProperty("group_id").GetString().Should().Be(a.GroupId);
        groups[0].EnumerateObject().Select(x => x.Name).Should().BeEquivalentTo(["group_id", "name", "member_count", "is_organizer"]);

        (await Api.Data(await b.Client.GetAsync($"/api/v1/tournaments/{id}"))).GetProperty("code").GetString().Should().Be(code);

        var (stranger, _) = await Api.RegisterAsync(_factory);
        var preview = await stranger.GetAsync($"/api/v1/tournaments/{code.ToLowerInvariant()}");
        preview.StatusCode.Should().Be(HttpStatusCode.OK, "con el código se ve la vista previa para decidir (F2-D67)");
        (await Api.Data(preview)).TryGetProperty("code", out _).Should().BeFalse();

        var list = await Api.Data(await b.Client.GetAsync("/api/v1/tournaments"));
        list.EnumerateArray().Select(x => x.GetProperty("id").GetString()).Should().Contain(id, "TOR-02 incluye los torneos donde mi grupo está inscrito");
    }

    // ---------------------------------------------------------------- TOR-05 (010-AC7)

    [Fact]
    public async Task AC7_CancelInProgress_RefundsEveryone_AndCancelsOpenMatches()
    {
        var a = await AdminGroupAsync("Cancela");
        var b = await AdminGroupAsync("Recupera");
        var (id, code) = await CreateAsync(a, entry: 40, size: 4);
        await JoinAsync(b.Client, code, b.GroupId, null, null);
        (await a.Client.PostAsync($"/api/v1/tournaments/{id}/start", null)).StatusCode.Should().Be(HttpStatusCode.OK);

        (await b.Client.PostAsync($"/api/v1/tournaments/{id}/cancel", null)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var cancelled = await a.Client.PostAsync($"/api/v1/tournaments/{id}/cancel", null);
        cancelled.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await Api.Data(cancelled);
        body.GetProperty("status").GetString().Should().Be("cancelled");
        body.GetProperty("prize_pool").GetInt32().Should().Be(0);

        Balance(a.GroupId, a.UserId).Should().Be(100);
        Balance(b.GroupId, b.UserId).Should().Be(100);
        var tid = ObjectId.Parse(id);
        _factory.Ledger.Entries.Where(x => x.RefId == tid).Sum(x => x.Amount).Should().Be(0, "cada cobro tiene su movimiento compensatorio");
        _factory.Matches.ForTournament(tid).Should().OnlyContain(x => x.Status == "cancelled" || x.Status == "bye");
        (await a.Client.PostAsync($"/api/v1/tournaments/{id}/cancel", null)).StatusCode.Should().Be(HttpStatusCode.Conflict);

        var notifications = await Api.Data(await b.Client.GetAsync("/api/v1/notifications"));
        notifications.EnumerateArray().Should().Contain(x => x.GetProperty("type").GetString() == "tournament_cancelled");
    }

    // ---------------------------------------------------------------- apoyo

    private sealed record AdminGroup(HttpClient Client, string UserId, string GroupId, string InviteCode);

    private async Task<AdminGroup> AdminGroupAsync(string name)
    {
        var (client, user) = await Api.RegisterAsync(_factory);
        var body = Api.Group(name);
        var created = await Api.Data(await client.PostAsJsonAsync("/api/v1/groups", body));
        return new AdminGroup(client, user.Id, created.GetProperty("id").GetString()!, created.GetProperty("invite_code").GetString()!);
    }

    private async Task<(string Id, string Code)> CreateAsync(AdminGroup admin, int entry, int size)
    {
        var response = await admin.Client.PostAsJsonAsync($"/api/v1/groups/{admin.GroupId}/tournaments", Body(entry, size));
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var data = await Api.Data(response);
        return (data.GetProperty("id").GetString()!, data.GetProperty("code").GetString()!);
    }

    private static Dictionary<string, object?> Body(int entry, int size, int days = 2) => new()
    {
        ["game"] = "Valorant",
        ["max_groups"] = size,
        ["entry_points"] = entry,
        ["scheduled_at"] = DateTimeOffset.UtcNow.AddDays(days).ToString("O")
    };

    private static async Task<HttpResponseMessage> JoinAsync(HttpClient client, string idOrCode, string groupId, string? code, string? key)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/tournaments/{idOrCode}/join")
        {
            Content = JsonContent.Create(new Dictionary<string, object?> { ["group_id"] = groupId, ["code"] = code })
        };
        if (key is not null)
        {
            request.Headers.Add("Idempotency-Key", key);
        }

        return await client.SendAsync(request);
    }

    private int Balance(string groupId, string userId) =>
        _factory.Members.Find(ObjectId.Parse(groupId), ObjectId.Parse(userId))!.PointsBalance;
}
