using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using MongoDB.Bson;
using Pactogg.Api.Modules.Tournaments;
using Pactogg.Api.Modules.Tournaments.Domain;
using SkiaSharp;
using Xunit;

namespace IntegrationTest;

/// <summary>Spec 010, parte 2: MAT-03..06, final con premio y trofeo, cancelación automática (R16) y TOR-09.</summary>
public sealed class Spec010MatchTests : IClassFixture<PactoggWebFactory>
{
    private readonly PactoggWebFactory _factory;

    public Spec010MatchTests(PactoggWebFactory factory)
    {
        _factory = factory;
    }

    // ---------------------------------------------------------------- 010-AC4 y 010-AC6

    [Fact]
    public async Task AC4_AC6_TieRejected_ReportAndRivalConfirm_WinnerAdvances_FinalSplitsThePoolAndAwardsTrophy()
    {
        var a = await AdminGroupAsync("Alfa");
        var b = await AdminGroupAsync("Beta");
        var c = await AdminGroupAsync("Gama");
        var d = await AdminGroupAsync("Delta");
        var (a2, a2User) = await Api.RegisterAsync(_factory);
        await Api.JoinAsync(a2, a.InviteCode);
        var all = new[] { a, b, c, d };
        var tid = await StartedAsync(a, entry: 35, others: [b, c, d]);

        var semiA = SemiOf(tid, a.GroupId);
        var rivalOfA = Rival(all, semiA, a.GroupId);
        var aIsP1 = semiA.P1GroupId == ObjectId.Parse(a.GroupId);

        var tie = await ReportAsync(a2, semiA.Id, 2, 2);
        tie.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await Api.Code(tie)).Should().Be("TIE_NOT_ALLOWED");
        var (outsider, _) = await Api.RegisterAsync(_factory);
        (await ReportAsync(outsider, semiA.Id, 3, 1)).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // a2 juega por Alfa en la semifinal y gana 3-1.
        var reported = await ReportAsync(a2, semiA.Id, aIsP1 ? 3 : 1, aIsP1 ? 1 : 3);
        reported.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await Api.Data(reported);
        body.GetProperty("status").GetString().Should().Be("reported");
        body.GetProperty("reported_by_group_id").GetString().Should().Be(a.GroupId);
        body.GetProperty("screenshot_url").GetString().Should().NotBeNullOrEmpty();
        _factory.Storage.Keys.Should().Contain(x => x.StartsWith($"evidence/matches/{tid}/"), "la foto cae bajo la regla de 30 días");
        (await ReportAsync(rivalOfA.Client, semiA.Id, 0, 1)).StatusCode.Should().Be(HttpStatusCode.Conflict, "ya hay un reporte");

        (await a2.PostAsync($"/api/v1/matches/{semiA.Id}/confirm", null)).StatusCode.Should().Be(HttpStatusCode.Forbidden, "quien reporta no confirma");
        (await a.Client.PostAsync($"/api/v1/matches/{semiA.Id}/confirm", null)).StatusCode.Should().Be(HttpStatusCode.Forbidden, "tampoco su propio grupo");
        (await Api.Data(await rivalOfA.Client.GetAsync("/api/v1/notifications"))).EnumerateArray()
            .Should().Contain(x => x.GetProperty("type").GetString() == "tournament_match_reported");

        var confirmed = await rivalOfA.Client.PostAsync($"/api/v1/matches/{semiA.Id}/confirm", null);
        confirmed.StatusCode.Should().Be(HttpStatusCode.OK);
        (await Api.Data(confirmed)).GetProperty("resolution").GetString().Should().Be("confirmed_by_rival");
        var final = _factory.Matches.ForTournament(tid).Single(x => x.NextMatchId is null);
        (semiA.Position % 2 == 0 ? final.P1GroupId : final.P2GroupId).Should().Be(ObjectId.Parse(a.GroupId), "el ganador avanza a su lugar en la final");

        // La otra semifinal: gana el P1.
        var other = _factory.Matches.ForTournament(tid).Single(x => x.Round == 1 && x.Id != semiA.Id);
        var otherP1 = all.Single(x => ObjectId.Parse(x.GroupId) == other.P1GroupId);
        var otherP2 = all.Single(x => ObjectId.Parse(x.GroupId) == other.P2GroupId);
        (await ReportAsync(otherP1.Client, other.Id, 2, 0)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await otherP2.Client.PostAsync($"/api/v1/matches/{other.Id}/confirm", null)).StatusCode.Should().Be(HttpStatusCode.OK);

        // Final: ahora juega el admin de Alfa y gana; confirma el finalista.
        final = _factory.Matches.ForTournament(tid).Single(x => x.NextMatchId is null);
        final.P1GroupId.Should().NotBeNull();
        final.P2GroupId.Should().NotBeNull();
        var finalIsP1 = final.P1GroupId == ObjectId.Parse(a.GroupId);
        (await ReportAsync(a.Client, final.Id, finalIsP1 ? 5 : 4, finalIsP1 ? 4 : 5)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await otherP1.Client.PostAsync($"/api/v1/matches/{final.Id}/confirm", null)).StatusCode.Should().Be(HttpStatusCode.OK);

        var t = await Api.Data(await a.Client.GetAsync($"/api/v1/tournaments/{tid}"));
        t.GetProperty("status").GetString().Should().Be("finished");
        t.GetProperty("winner_group_id").GetString().Should().Be(a.GroupId);
        var prizes = _factory.Ledger.Entries.Where(x => x.RefId == tid && x.Type == "tournament_prize").ToList();
        prizes.Sum(x => x.Amount).Should().Be(140, "suma de premios = bolsa (4 × 35)");
        prizes.Should().HaveCount(2, "los dos que jugaron por Alfa");
        Balance(a.GroupId, a2User.Id).Should().Be(100 + 70);
        Balance(a.GroupId, a.UserId).Should().Be(100 - 35 + 70);
        Balance(b.GroupId, b.UserId).Should().Be(100 - 35);
        _factory.Achievements.All.Where(x => x.SourceId == tid && x.Code == "campeon_torneo").Select(x => x.UserId.ToString())
            .Should().BeEquivalentTo([a.UserId, a2User.Id]);
        (await Api.Data(await b.Client.GetAsync("/api/v1/notifications"))).EnumerateArray()
            .Should().Contain(x => x.GetProperty("type").GetString() == "tournament_finished");
        (await ReportAsync(a.Client, final.Id, 1, 0)).StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Theory]
    [InlineData(140, new[] { 70, 70 })]
    [InlineData(101, new[] { 35, 33, 33 })]
    [InlineData(0, new int[0])]
    public void AC6_Split_EqualParts_RemainderToTheFirst(int pool, int[] expected)
    {
        var people = expected.Length == 0 ? [ObjectId.GenerateNewId()] : expected.Select(_ => ObjectId.GenerateNewId()).ToList();
        var split = MatchService.Split(pool, people);
        split.Select(x => x.Amount).Should().Equal(expected);
        split.Sum(x => x.Amount).Should().Be(pool);
    }

    // ---------------------------------------------------------------- 010-AC5

    [Fact]
    public async Task AC5_Dispute_OrganizerResolves_AndCannotResolveTheMatchHePlays()
    {
        var o = await AdminGroupAsync("Organiza");
        var b = await AdminGroupAsync("Bravo");
        var c = await AdminGroupAsync("Charlie");
        var d = await AdminGroupAsync("Dorado");
        var all = new[] { o, b, c, d };
        var tid = await StartedAsync(o, entry: 0, others: [b, c, d]);

        var semiO = SemiOf(tid, o.GroupId);
        var rivalOfO = Rival(all, semiO, o.GroupId);
        var oIsP1 = semiO.P1GroupId == ObjectId.Parse(o.GroupId);
        (await ReportAsync(rivalOfO.Client, semiO.Id, oIsP1 ? 0 : 1, oIsP1 ? 1 : 0)).StatusCode.Should().Be(HttpStatusCode.OK);
        var own = await o.Client.PostAsJsonAsync($"/api/v1/matches/{semiO.Id}/resolve", Winner(o.GroupId));
        own.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await Api.Code(own)).Should().Be("ORGANIZER_PLAYS_MATCH", "CLARIFICACION #105");

        var other = _factory.Matches.ForTournament(tid).Single(x => x.Round == 1 && x.Id != semiO.Id);
        var p1 = all.Single(x => ObjectId.Parse(x.GroupId) == other.P1GroupId);
        var p2 = all.Single(x => ObjectId.Parse(x.GroupId) == other.P2GroupId);
        (await ReportAsync(p1.Client, other.Id, 3, 0)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await p2.Client.PostAsJsonAsync($"/api/v1/matches/{other.Id}/dispute", Reason("no"))).StatusCode
            .Should().Be(HttpStatusCode.UnprocessableEntity, "el motivo lleva de 5 a 140 caracteres");
        (await p1.Client.PostAsJsonAsync($"/api/v1/matches/{other.Id}/dispute", Reason("Me reporto yo mismo"))).StatusCode
            .Should().Be(HttpStatusCode.Forbidden);
        var disputed = await p2.Client.PostAsJsonAsync($"/api/v1/matches/{other.Id}/dispute", Reason("Ese marcador no es real"));
        disputed.StatusCode.Should().Be(HttpStatusCode.OK);
        (await Api.Data(disputed)).GetProperty("status").GetString().Should().Be("disputed");
        (await Api.Data(await o.Client.GetAsync("/api/v1/notifications"))).EnumerateArray()
            .Should().Contain(x => x.GetProperty("type").GetString() == "tournament_match_disputed");
        (await p2.Client.PostAsync($"/api/v1/matches/{other.Id}/confirm", null)).StatusCode.Should().Be(HttpStatusCode.Conflict, "en disputa decide el organizador");

        (await b.Client.PostAsJsonAsync($"/api/v1/matches/{other.Id}/resolve", Winner(p2.GroupId))).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await o.Client.PostAsJsonAsync($"/api/v1/matches/{other.Id}/resolve", Winner(o.GroupId))).StatusCode
            .Should().Be(HttpStatusCode.UnprocessableEntity, "el ganador es uno de los dos grupos de la partida");
        var resolved = await o.Client.PostAsJsonAsync($"/api/v1/matches/{other.Id}/resolve", Winner(p2.GroupId));
        resolved.StatusCode.Should().Be(HttpStatusCode.OK);
        var match = await Api.Data(resolved);
        match.GetProperty("status").GetString().Should().Be("confirmed");
        match.GetProperty("resolution").GetString().Should().Be("resolved_dispute");
        match.GetProperty("winner_group_id").GetString().Should().Be(p2.GroupId);
        match.TryGetProperty("p1_score", out _).Should().BeFalse("si el organizador contradice el reporte, el marcador se borra");
        var final = _factory.Matches.ForTournament(tid).Single(x => x.NextMatchId is null);
        (other.Position % 2 == 0 ? final.P1GroupId : final.P2GroupId).Should().Be(ObjectId.Parse(p2.GroupId));
        (await o.Client.PostAsJsonAsync($"/api/v1/matches/{other.Id}/resolve", Winner(p2.GroupId))).StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task AC5_ReportWithoutAnswer24h_OrganizerConfirms_AbandonmentInTheFinal_PrizeToWhoPlayed()
    {
        var o = await AdminGroupAsync("Sede");
        var b = await AdminGroupAsync("Norte");
        var c = await AdminGroupAsync("Sur");
        var d = await AdminGroupAsync("Este");
        var all = new[] { o, b, c, d };
        var tid = await StartedAsync(o, entry: 10, others: [b, c, d]);

        var other = _factory.Matches.ForTournament(tid).Single(x => x.Round == 1 && x.P1GroupId != ObjectId.Parse(o.GroupId) && x.P2GroupId != ObjectId.Parse(o.GroupId));
        var p1 = all.Single(x => ObjectId.Parse(x.GroupId) == other.P1GroupId);
        (await ReportAsync(p1.Client, other.Id, 1, 0)).StatusCode.Should().Be(HttpStatusCode.OK);
        var early = await o.Client.PostAsJsonAsync($"/api/v1/matches/{other.Id}/resolve", Winner(p1.GroupId));
        early.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await Api.Code(early)).Should().Be("REPORT_TOO_RECENT");

        _factory.Matches.ForTournament(tid).Single(x => x.Id == other.Id).ReportedAt = DateTime.UtcNow.AddHours(-25);
        var late = await Api.Data(await o.Client.PostAsJsonAsync($"/api/v1/matches/{other.Id}/resolve", Winner(p1.GroupId)));
        late.GetProperty("resolution").GetString().Should().Be("resolved_no_answer");
        late.GetProperty("p1_score").GetInt32().Should().Be(1, "si coincide con el reporte, el marcador se conserva");

        // El organizador pierde su semifinal; la final no lo incluye.
        var semiO = SemiOf(tid, o.GroupId);
        var rivalOfO = Rival(all, semiO, o.GroupId);
        var oIsP1 = semiO.P1GroupId == ObjectId.Parse(o.GroupId);
        (await ReportAsync(rivalOfO.Client, semiO.Id, oIsP1 ? 0 : 2, oIsP1 ? 2 : 0)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await o.Client.PostAsync($"/api/v1/matches/{semiO.Id}/confirm", null)).StatusCode.Should().Be(HttpStatusCode.OK);

        // Nadie reporta la final: el organizador declara abandono a favor de p1.
        var final = _factory.Matches.ForTournament(tid).Single(x => x.NextMatchId is null);
        var abandoned = await Api.Data(await o.Client.PostAsJsonAsync($"/api/v1/matches/{final.Id}/resolve", new Dictionary<string, object?>
        {
            ["winner_group_id"] = p1.GroupId,
            ["reason"] = "El rival no se presentó"
        }));
        abandoned.GetProperty("resolution").GetString().Should().Be("abandonment");
        var t = await Api.Data(await p1.Client.GetAsync($"/api/v1/tournaments/{tid}"));
        t.GetProperty("status").GetString().Should().Be("finished");
        _factory.Ledger.Entries.Where(x => x.RefId == tid && x.Type == "tournament_prize").Should()
            .ContainSingle(x => x.UserId == ObjectId.Parse(p1.UserId) && x.Amount == 40, "quien jugó por el campeón en rondas anteriores cobra la bolsa");
    }

    // ---------------------------------------------------------------- 010-AC7 (R16)

    [Fact]
    public async Task AC7_GroupDeleted_LeavesLessThan2_CancelsAndRefunds_With3GroupsKeepsGoing()
    {
        var a = await AdminGroupAsync("Queda");
        var b = await AdminGroupAsync("Se va");
        var tid = await StartedAsync(a, entry: 40, others: [b]);
        (await b.Client.DeleteAsync($"/api/v1/groups/{b.GroupId}")).StatusCode.Should().Be(HttpStatusCode.NoContent);

        var t = await Api.Data(await a.Client.GetAsync($"/api/v1/tournaments/{tid}"));
        t.GetProperty("status").GetString().Should().Be("cancelled");
        t.GetProperty("cancel_reason").GetString().Should().Be("not_enough_groups");
        Balance(a.GroupId, a.UserId).Should().Be(100);
        _factory.Ledger.Entries.Where(x => x.RefId == tid).Sum(x => x.Amount).Should().Be(0);

        var x = await AdminGroupAsync("Uno de tres");
        var y = await AdminGroupAsync("Dos de tres");
        var z = await AdminGroupAsync("Tres de tres");
        var tid2 = await StartedAsync(x, entry: 0, others: [y, z]);
        (await z.Client.DeleteAsync($"/api/v1/groups/{z.GroupId}")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await Api.Data(await x.Client.GetAsync($"/api/v1/tournaments/{tid2}"))).GetProperty("status").GetString()
            .Should().Be("in_progress", "con 2 grupos activos sigue; el organizador resuelve por abandono");
    }

    // ---------------------------------------------------------------- 010-R8 (TOR-09)

    [Fact]
    public async Task R8_Invite_OnlyNotifiesAdminsOfGroupsWhereTheOrganizerIsMember()
    {
        var o = await AdminGroupAsync("Anfitrión");
        var shared = await AdminGroupAsync("Compartido");
        var foreign = await AdminGroupAsync("Ajeno");
        await Api.JoinAsync(o.Client, shared.InviteCode);
        var created = await Api.Data(await o.Client.PostAsJsonAsync($"/api/v1/groups/{o.GroupId}/tournaments", Body(20)));
        var tid = created.GetProperty("id").GetString();

        (await shared.Client.PostAsJsonAsync($"/api/v1/tournaments/{tid}/invite", Groups(shared.GroupId))).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await o.Client.PostAsJsonAsync($"/api/v1/tournaments/{tid}/invite", Groups())).StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);

        var response = await o.Client.PostAsJsonAsync($"/api/v1/tournaments/{tid}/invite", Groups(shared.GroupId, foreign.GroupId, o.GroupId, "nada"));
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await Api.Data(response);
        body.GetProperty("invited").EnumerateArray().Select(x => x.GetString()).Should().Equal(shared.GroupId);
        body.GetProperty("code").GetString().Should().Be(created.GetProperty("code").GetString());
        var skipped = body.GetProperty("skipped").EnumerateArray().ToDictionary(x => x.GetProperty("group_id").GetString()!, x => x.GetProperty("reason").GetString());
        skipped[foreign.GroupId].Should().Be("not_member", "010-D3: los grupos no se ven entre sí");
        skipped[o.GroupId].Should().Be("already_joined");
        skipped["nada"].Should().Be("not_found");

        (await Api.Data(await shared.Client.GetAsync("/api/v1/notifications"))).EnumerateArray()
            .Should().Contain(x => x.GetProperty("type").GetString() == "tournament_invite");
        (await Api.Data(await foreign.Client.GetAsync("/api/v1/notifications"))).EnumerateArray()
            .Should().NotContain(x => x.GetProperty("type").GetString() == "tournament_invite");

        await JoinAsync(shared.Client, created.GetProperty("code").GetString()!, shared.GroupId);
        (await o.Client.PostAsync($"/api/v1/tournaments/{tid}/start", null)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await o.Client.PostAsJsonAsync($"/api/v1/tournaments/{tid}/invite", Groups(shared.GroupId))).StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    // ---------------------------------------------------------------- apoyo

    private sealed record AdminGroup(HttpClient Client, string UserId, string GroupId, string InviteCode);

    private async Task<AdminGroup> AdminGroupAsync(string name)
    {
        var (client, user) = await Api.RegisterAsync(_factory);
        var created = await Api.Data(await client.PostAsJsonAsync("/api/v1/groups", Api.Group(name)));
        return new AdminGroup(client, user.Id, created.GetProperty("id").GetString()!, created.GetProperty("invite_code").GetString()!);
    }

    private async Task<ObjectId> StartedAsync(AdminGroup organizer, int entry, AdminGroup[] others)
    {
        var response = await organizer.Client.PostAsJsonAsync($"/api/v1/groups/{organizer.GroupId}/tournaments", Body(entry));
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var data = await Api.Data(response);
        var code = data.GetProperty("code").GetString()!;
        foreach (var other in others)
        {
            (await JoinAsync(other.Client, code, other.GroupId)).StatusCode.Should().Be(HttpStatusCode.OK);
        }

        var id = data.GetProperty("id").GetString()!;
        (await organizer.Client.PostAsync($"/api/v1/tournaments/{id}/start", null)).StatusCode.Should().Be(HttpStatusCode.OK);
        return ObjectId.Parse(id);
    }

    private Match SemiOf(ObjectId tid, string groupId)
    {
        var gid = ObjectId.Parse(groupId);
        return _factory.Matches.ForTournament(tid).Single(x => x.Round == 1 && (x.P1GroupId == gid || x.P2GroupId == gid));
    }

    private static AdminGroup Rival(AdminGroup[] all, Match match, string groupId)
    {
        var gid = ObjectId.Parse(groupId);
        var rival = match.P1GroupId == gid ? match.P2GroupId : match.P1GroupId;
        return all.Single(x => ObjectId.Parse(x.GroupId) == rival);
    }

    private static async Task<HttpResponseMessage> ReportAsync(HttpClient client, ObjectId matchId, int p1, int p2)
    {
        using var bitmap = new SKBitmap(4, 4);
        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(SKColors.SeaGreen);
        }

        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        using var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(data.ToArray());
        file.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        form.Add(file, "file", "resultado.png");
        form.Add(new StringContent(p1.ToString()), "p1_score");
        form.Add(new StringContent(p2.ToString()), "p2_score");
        return await client.PostAsync($"/api/v1/matches/{matchId}/report", form);
    }

    private static async Task<HttpResponseMessage> JoinAsync(HttpClient client, string code, string groupId)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/tournaments/{code}/join")
        {
            Content = JsonContent.Create(new Dictionary<string, object?> { ["group_id"] = groupId, ["code"] = code })
        };
        return await client.SendAsync(request);
    }

    private static Dictionary<string, object?> Body(int entry) => new()
    {
        ["game"] = "Valorant",
        ["max_groups"] = 4,
        ["entry_points"] = entry,
        ["scheduled_at"] = DateTimeOffset.UtcNow.AddDays(2).ToString("O")
    };

    private static Dictionary<string, object?> Winner(string groupId) => new() { ["winner_group_id"] = groupId };

    private static Dictionary<string, object?> Reason(string reason) => new() { ["reason"] = reason };

    private static Dictionary<string, object?> Groups(params string[] ids) => new() { ["group_ids"] = ids };

    private int Balance(string groupId, string userId) =>
        _factory.Members.Find(ObjectId.Parse(groupId), ObjectId.Parse(userId))!.PointsBalance;
}
