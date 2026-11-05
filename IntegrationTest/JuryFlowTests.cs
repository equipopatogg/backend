using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using MongoDB.Bson;

namespace IntegrationTest;

/// <summary>EVI-03, VOT-01..05, PTS-01/02, COM-01/03/04, NOT-01..03, ACH-01.</summary>
public sealed class JuryFlowTests : IClassFixture<PactoggWebFactory>
{
    private readonly PactoggWebFactory _factory;

    public JuryFlowTests(PactoggWebFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task FulfilledContract_ReleasesBond_CreatesCommitment_AndShowsInFame()
    {
        var (owner, ownerUser) = await NewUserAsync();
        var (doer, doerUser) = await NewUserAsync();
        var jurors = new List<(HttpClient Client, TestUser User)>();
        var (groupId, code) = await CreateGroupAsync(owner, jurors: 3);
        await JoinAsync(doer, code);
        for (var i = 0; i < 3; i++)
        {
            var juror = await NewUserAsync();
            await JoinAsync(juror.Client, code);
            jurors.Add(juror);
        }

        var contractId = await CreateContractAsync(owner, groupId, 50, doerUser.Id);
        (await Data(await doer.GetAsync("/api/v1/notifications"))).EnumerateArray()
            .Should().Contain(x => x.GetProperty("type").GetString() == "contract_received");

        (await doer.PostAsync($"/api/v1/contracts/{contractId}/accept", null)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await doer.PostAsJsonAsync($"/api/v1/contracts/{contractId}/evidence", new Dictionary<string, object?>
        {
            ["type"] = "link",
            ["url"] = "https://www.youtube.com/watch?v=jurado1"
        })).StatusCode.Should().Be(HttpStatusCode.Created);

        var contract = _factory.Contracts.Get(ObjectId.Parse(contractId));
        contract.Status.Should().Be("voting");
        contract.Jurors.Should().HaveCount(3).And.NotContain(ObjectId.Parse(ownerUser.Id)).And.NotContain(ObjectId.Parse(doerUser.Id));

        var (j1, j2, j3) = (jurors[0].Client, jurors[1].Client, jurors[2].Client);
        (await Data(await j1.GetAsync("/api/v1/me/votes/pending"))).GetArrayLength().Should().Be(1);
        (await Data(await j1.GetAsync("/api/v1/notifications?unread=true"))).EnumerateArray()
            .Should().Contain(x => x.GetProperty("type").GetString() == "vote_requested");

        var evidence = await Data(await j1.GetAsync($"/api/v1/contracts/{contractId}/evidence"));
        evidence.GetArrayLength().Should().Be(1);
        evidence[0].GetProperty("url").GetString().Should().Contain("youtube.com");

        var summary = await Data(await j1.GetAsync($"/api/v1/contracts/{contractId}/votes/summary"));
        summary.GetProperty("jurors_assigned").GetInt32().Should().Be(3);
        summary.GetProperty("i_am_juror").GetBoolean().Should().BeTrue();
        summary.TryGetProperty("legit_votes", out _).Should().BeFalse();

        var notJuror = await owner.PutAsJsonAsync($"/api/v1/contracts/{contractId}/votes", Vote("legit"));
        notJuror.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await Code(notJuror)).Should().Be("NOT_JUROR");

        var first = await Data(await j1.PutAsJsonAsync($"/api/v1/contracts/{contractId}/votes", Vote("legit", "Se ve limpio")));
        first.GetProperty("status").GetString().Should().Be("voting");
        first.GetProperty("votes_cast").GetInt32().Should().Be(1);

        var again = await j1.PutAsJsonAsync($"/api/v1/contracts/{contractId}/votes", Vote("cheat"));
        again.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await Code(again)).Should().Be("ALREADY_VOTED");

        (await j1.GetAsync($"/api/v1/contracts/{contractId}/result")).StatusCode.Should().Be(HttpStatusCode.Conflict);
        var early = await owner.PostAsJsonAsync($"/api/v1/contracts/{contractId}/votes/resolve", new Dictionary<string, object?> { ["outcome"] = "cheating" });
        early.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await Code(early)).Should().Be("VOTING_NOT_FINISHED");

        var second = await Data(await j2.PutAsJsonAsync($"/api/v1/contracts/{contractId}/votes", Vote("legit")));
        second.GetProperty("status").GetString().Should().Be("fulfilled");
        (await j3.PutAsJsonAsync($"/api/v1/contracts/{contractId}/votes", Vote("cheat"))).StatusCode.Should().Be(HttpStatusCode.Conflict);

        var result = await Data(await doer.GetAsync($"/api/v1/contracts/{contractId}/result"));
        result.GetProperty("outcome").GetString().Should().Be("fulfilled");
        result.GetProperty("resolved_by").GetString().Should().Be("jury");
        result.GetProperty("bond_movement").GetString().Should().Be("bond_release");
        result.GetProperty("votes").GetArrayLength().Should().Be(2);

        var points = await ReadJsonAsync(await doer.GetAsync($"/api/v1/groups/{groupId}/points/me"));
        points.GetProperty("data").GetProperty("balance").GetInt32().Should().Be(100);
        points.GetProperty("data").GetProperty("movements").EnumerateArray().Select(x => x.GetProperty("type").GetString())
            .Should().BeEquivalentTo(["bond_release", "bond_lock"]);
        (await Data(await doer.GetAsync($"/api/v1/groups/{groupId}/points/me?type=bond_lock"))).GetProperty("movements").GetArrayLength().Should().Be(1);
        (await doer.GetAsync($"/api/v1/groups/{groupId}/points/me?type=compra")).StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);

        var ranking = await Data(await j1.GetAsync($"/api/v1/groups/{groupId}/points/ranking"));
        ranking.GetArrayLength().Should().Be(5);
        ranking[0].GetProperty("position").GetInt32().Should().Be(1);

        var owed = await Data(await owner.GetAsync($"/api/v1/groups/{groupId}/commitments?role=debo"));
        owed.GetArrayLength().Should().Be(1);
        var commitmentId = owed[0].GetProperty("id").GetString();
        owed[0].GetProperty("creditor").GetProperty("nickname").GetString().Should().Be(doerUser.Nickname);
        (await Data(await doer.GetAsync($"/api/v1/groups/{groupId}/commitments?role=me-deben"))).GetArrayLength().Should().Be(1);
        (await doer.GetAsync($"/api/v1/groups/{groupId}/commitments?role=otro")).StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);

        var wrongParty = await doer.PostAsync($"/api/v1/commitments/{commitmentId}/request-complete", null);
        wrongParty.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await Data(await owner.PostAsync($"/api/v1/commitments/{commitmentId}/request-complete", null)))
            .GetProperty("status").GetString().Should().Be("completion_requested");
        (await Data(await doer.PostAsync($"/api/v1/commitments/{commitmentId}/complete", null)))
            .GetProperty("status").GetString().Should().Be("completed");
        var twice = await doer.PostAsync($"/api/v1/commitments/{commitmentId}/complete", null);
        twice.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await Code(twice)).Should().Be("INVALID_COMMITMENT_STATE");

        var fame = await Data(await j3.GetAsync($"/api/v1/groups/{groupId}/fame"));
        fame.GetArrayLength().Should().Be(1);
        fame[0].GetProperty("executor").GetProperty("nickname").GetString().Should().Be(doerUser.Nickname);
        fame[0].GetProperty("links").GetArrayLength().Should().Be(1);

        (await Data(await doer.GetAsync("/api/v1/users/me"))).GetProperty("stats").GetProperty("completed").GetInt32().Should().Be(1);

        var inbox = await ReadJsonAsync(await doer.GetAsync("/api/v1/notifications"));
        inbox.GetProperty("meta").GetProperty("unread_count").GetInt64().Should().BeGreaterThan(0);
        var notificationId = inbox.GetProperty("data")[0].GetProperty("id").GetString();
        (await Data(await doer.PatchAsync($"/api/v1/notifications/{notificationId}/read", null))).GetProperty("read").GetBoolean().Should().BeTrue();
        (await owner.PatchAsync($"/api/v1/notifications/{notificationId}/read", null)).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await Data(await doer.PostAsync("/api/v1/notifications/read-all", null))).GetProperty("updated").GetInt64().Should().BeGreaterThan(0);
        (await ReadJsonAsync(await doer.GetAsync("/api/v1/notifications"))).GetProperty("meta").GetProperty("unread_count").GetInt64().Should().Be(0);
    }

    [Fact]
    public async Task CheatingContract_ForfeitsBondToChallenger()
    {
        var (owner, ownerUser) = await NewUserAsync();
        var (doer, doerUser) = await NewUserAsync();
        var (juror, _) = await NewUserAsync();
        var (groupId, code) = await CreateGroupAsync(owner, jurors: 1);
        await JoinAsync(doer, code);
        await JoinAsync(juror, code);

        var contractId = await CreateContractAsync(owner, groupId, 25, doerUser.Id);
        (await doer.PostAsync($"/api/v1/contracts/{contractId}/accept", null)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await doer.PostAsJsonAsync($"/api/v1/contracts/{contractId}/evidence", new Dictionary<string, object?>
        {
            ["type"] = "link",
            ["url"] = "https://www.twitch.tv/videos/123"
        })).StatusCode.Should().Be(HttpStatusCode.Created);

        var vote = await Data(await juror.PutAsJsonAsync($"/api/v1/contracts/{contractId}/votes", Vote("cheat")));
        vote.GetProperty("status").GetString().Should().Be("cheating");

        (await Data(await owner.GetAsync($"/api/v1/groups/{groupId}/points/me"))).GetProperty("balance").GetInt32().Should().Be(125);
        (await Data(await doer.GetAsync($"/api/v1/groups/{groupId}/points/me"))).GetProperty("balance").GetInt32().Should().Be(75);
        (await Data(await doer.GetAsync($"/api/v1/contracts/{contractId}/result"))).GetProperty("bond_movement").GetString().Should().Be("bond_forfeit");
        (await Data(await owner.GetAsync($"/api/v1/groups/{groupId}/commitments?role=debo"))).GetArrayLength().Should().Be(0);
        (await Data(await doer.GetAsync("/api/v1/users/me"))).GetProperty("stats").GetProperty("cheated").GetInt32().Should().Be(1);
        (await Data(await owner.GetAsync($"/api/v1/groups/{groupId}/fame"))).GetArrayLength().Should().Be(0);
        _factory.Ledger.Entries.Should().Contain(x =>
            x.RefId == ObjectId.Parse(contractId) && x.Type == "bond_forfeit" && x.UserId == ObjectId.Parse(ownerUser.Id) && x.Amount == 25);
    }

    [Fact]
    public async Task TiedVote_IsResolvedOnlyByAdmin()
    {
        var (owner, _) = await NewUserAsync();
        var (doer, doerUser) = await NewUserAsync();
        var (j1, _) = await NewUserAsync();
        var (j2, _) = await NewUserAsync();
        var (groupId, code) = await CreateGroupAsync(owner, jurors: 2);
        foreach (var client in new[] { doer, j1, j2 })
        {
            await JoinAsync(client, code);
        }

        var contractId = await CreateContractAsync(owner, groupId, 25, doerUser.Id);
        (await doer.PostAsync($"/api/v1/contracts/{contractId}/accept", null)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await doer.PostAsJsonAsync($"/api/v1/contracts/{contractId}/evidence", new Dictionary<string, object?>
        {
            ["type"] = "link",
            ["url"] = "https://medal.tv/clips/abc"
        })).StatusCode.Should().Be(HttpStatusCode.Created);

        (await Data(await j1.PutAsJsonAsync($"/api/v1/contracts/{contractId}/votes", Vote("legit")))).GetProperty("needs_admin").GetBoolean().Should().BeFalse();
        var tie = await Data(await j2.PutAsJsonAsync($"/api/v1/contracts/{contractId}/votes", Vote("cheat")));
        tie.GetProperty("status").GetString().Should().Be("voting");
        tie.GetProperty("needs_admin").GetBoolean().Should().BeTrue();
        (await Data(await owner.GetAsync($"/api/v1/contracts/{contractId}/votes/summary"))).GetProperty("admin_can_resolve").GetBoolean().Should().BeTrue();

        var notAdmin = await j1.PostAsJsonAsync($"/api/v1/contracts/{contractId}/votes/resolve", new Dictionary<string, object?> { ["outcome"] = "fulfilled" });
        notAdmin.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await Code(notAdmin)).Should().Be("NOT_GROUP_ADMIN");
        (await owner.PostAsJsonAsync($"/api/v1/contracts/{contractId}/votes/resolve", new Dictionary<string, object?> { ["outcome"] = "maybe" }))
            .StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);

        var resolved = await Data(await owner.PostAsJsonAsync($"/api/v1/contracts/{contractId}/votes/resolve", new Dictionary<string, object?> { ["outcome"] = "fulfilled" }));
        resolved.GetProperty("outcome").GetString().Should().Be("fulfilled");
        resolved.GetProperty("resolved_by").GetString().Should().Be("admin");
        resolved.GetProperty("legit_votes").GetInt32().Should().Be(1);
        resolved.GetProperty("cheat_votes").GetInt32().Should().Be(1);
        (await Data(await doer.GetAsync($"/api/v1/groups/{groupId}/points/me"))).GetProperty("balance").GetInt32().Should().Be(100);
    }

    [Fact]
    public async Task Outsider_CannotSeeEvidencePointsCommitmentsOrFame()
    {
        var (owner, _) = await NewUserAsync();
        var (outsider, _) = await NewUserAsync();
        var (groupId, _) = await CreateGroupAsync(owner, jurors: 3);
        var contractId = await CreateContractAsync(owner, groupId, 25, null);

        foreach (var path in new[]
                 {
                     $"/api/v1/contracts/{contractId}/evidence",
                     $"/api/v1/contracts/{contractId}/votes/summary",
                     $"/api/v1/groups/{groupId}/points/me",
                     $"/api/v1/groups/{groupId}/points/ranking",
                     $"/api/v1/groups/{groupId}/commitments",
                     $"/api/v1/groups/{groupId}/fame"
                 })
        {
            var response = await outsider.GetAsync(path);
            response.StatusCode.Should().Be(HttpStatusCode.Forbidden, path);
        }

        (await outsider.PostAsync($"/api/v1/commitments/{ObjectId.GenerateNewId()}/complete", null)).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await outsider.GetAsync("/api/v1/me/votes/pending")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    private static Dictionary<string, object?> Vote(string value, string? comment = null) =>
        new() { ["vote"] = value, ["comment"] = comment };

    private sealed record TestUser(string Id, string Nickname);

    private async Task<(HttpClient Client, TestUser User)> NewUserAsync()
    {
        var client = _factory.CreateClient();
        var nickname = $"u{Guid.NewGuid():N}"[..20];
        var response = await client.PostAsJsonAsync("/api/v1/auth/register", new Dictionary<string, object?>
        {
            ["email"] = $"user-{Guid.NewGuid():N}@example.com",
            ["password"] = "frase larga de ejemplo",
            ["nickname"] = nickname,
            ["accept_privacy"] = true,
            ["privacy_version"] = "2026-10-01"
        });
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var data = (await ReadJsonAsync(response)).GetProperty("data");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", data.GetProperty("access_token").GetString());
        return (client, new TestUser(data.GetProperty("user").GetProperty("id").GetString()!, nickname));
    }

    private static async Task<(string Id, string Code)> CreateGroupAsync(HttpClient client, int jurors)
    {
        var response = await client.PostAsJsonAsync("/api/v1/groups", new Dictionary<string, object?>
        {
            ["name"] = "Jurado",
            ["settings"] = new Dictionary<string, object?> { ["jurors"] = jurors, ["consensus"] = 0.66, ["bond_options"] = new[] { 25, 50, 100 } }
        });
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var data = (await ReadJsonAsync(response)).GetProperty("data");
        return (data.GetProperty("id").GetString()!, data.GetProperty("invite_code").GetString()!);
    }

    private static async Task JoinAsync(HttpClient client, string code) =>
        (await client.PostAsJsonAsync("/api/v1/groups/join", new Dictionary<string, object?> { ["code"] = code }))
            .StatusCode.Should().Be(HttpStatusCode.OK);

    private static async Task<string> CreateContractAsync(HttpClient client, string groupId, int bond, string? targetId)
    {
        var response = await client.PostAsJsonAsync($"/api/v1/groups/{groupId}/contracts", new Dictionary<string, object?>
        {
            ["game"] = "Valorant",
            ["condition"] = "Ganar tres partidas seguidas",
            ["reward"] = "Unos tacos al pastor",
            ["deadline"] = DateTimeOffset.UtcNow.AddDays(7).ToString("O"),
            ["bond_required"] = bond,
            ["target_type"] = targetId is null ? "group" : "member",
            ["target_id"] = targetId,
            ["source"] = "manual"
        });
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await ReadJsonAsync(response)).GetProperty("data").GetProperty("id").GetString()!;
    }

    private static async Task<JsonElement> Data(HttpResponseMessage response)
    {
        var json = await ReadJsonAsync(response);
        json.TryGetProperty("data", out var data).Should().BeTrue($"se esperaba data y llegó {(int)response.StatusCode}: {json}");
        return data;
    }

    private static async Task<string?> Code(HttpResponseMessage response) =>
        (await ReadJsonAsync(response)).GetProperty("code").GetString();

    private static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response)
    {
        var stream = await response.Content.ReadAsStreamAsync();
        using var document = await JsonDocument.ParseAsync(stream);
        return document.RootElement.Clone();
    }
}
