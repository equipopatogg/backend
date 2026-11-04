using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using MongoDB.Bson;

namespace IntegrationTest;

/// <summary>USR-02/03/06, GRP-02/03, MEM-01/02, CON-02/03/06/07.</summary>
public sealed class SocialFlowTests : IClassFixture<PactoggWebFactory>
{
    private readonly PactoggWebFactory _factory;

    public SocialFlowTests(PactoggWebFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Profile_GetUpdateAndCheckNickname()
    {
        var (client, user) = await NewUserAsync();
        var (_, other) = await NewUserAsync();

        var me = await client.GetAsync("/api/v1/users/me");
        me.StatusCode.Should().Be(HttpStatusCode.OK);
        var meData = (await ReadJsonAsync(me)).GetProperty("data");
        meData.GetProperty("nickname").GetString().Should().Be(user.Nickname);
        meData.GetProperty("stats").GetProperty("word_index").GetInt32().Should().Be(50);
        meData.GetProperty("groups_count").GetInt32().Should().Be(0);
        meData.GetProperty("avatar_id").GetString().Should().Be("default");

        var anonymous = _factory.CreateClient();
        var taken = await anonymous.GetAsync($"/api/v1/users/check-nickname?nickname={user.Nickname.ToUpperInvariant()}");
        taken.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ReadJsonAsync(taken)).GetProperty("data").GetProperty("available").GetBoolean().Should().BeFalse();

        var freeNick = $"n{Guid.NewGuid():N}"[..16];
        var free = await anonymous.GetAsync($"/api/v1/users/check-nickname?nickname={freeNick}");
        (await ReadJsonAsync(free)).GetProperty("data").GetProperty("available").GetBoolean().Should().BeTrue();

        var invalid = await anonymous.GetAsync("/api/v1/users/check-nickname?nickname=a");
        invalid.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);

        var patch = await client.PatchAsJsonAsync("/api/v1/users/me", new Dictionary<string, object?>
        {
            ["nickname"] = freeNick,
            ["avatar_id"] = "avatar_03"
        });
        patch.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ReadJsonAsync(patch)).GetProperty("data").GetProperty("nickname").GetString().Should().Be(freeNick);

        var refreshed = (await ReadJsonAsync(await client.GetAsync("/api/v1/users/me"))).GetProperty("data");
        refreshed.GetProperty("nickname").GetString().Should().Be(freeNick);
        refreshed.GetProperty("avatar_id").GetString().Should().Be("avatar_03");

        var duplicate = await client.PatchAsJsonAsync("/api/v1/users/me", new Dictionary<string, object?> { ["nickname"] = other.Nickname });
        duplicate.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await ReadJsonAsync(duplicate)).GetProperty("code").GetString().Should().Be("NICKNAME_ALREADY_EXISTS");

        var empty = await client.PatchAsJsonAsync("/api/v1/users/me", new Dictionary<string, object?>());
        empty.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);

        var unknown = await client.PatchAsJsonAsync("/api/v1/users/me", new Dictionary<string, object?> { ["email"] = "x@example.com" });
        unknown.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var moderated = await client.PatchAsJsonAsync("/api/v1/users/me", new Dictionary<string, object?> { ["nickname"] = "reject_me" });
        moderated.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await ReadJsonAsync(moderated)).GetProperty("code").GetString().Should().Be("CONTENT_REJECTED");
    }

    [Fact]
    public async Task Groups_JoinListDetailAndMembers()
    {
        var (owner, ownerUser) = await NewUserAsync();
        var (friend, _) = await NewUserAsync();
        var (outsider, _) = await NewUserAsync();
        var (groupId, inviteCode) = await CreateGroupAsync(owner, "Los Compas");

        var join = await friend.PostAsJsonAsync("/api/v1/groups/join", new Dictionary<string, object?> { ["code"] = inviteCode.ToLowerInvariant() });
        join.StatusCode.Should().Be(HttpStatusCode.OK);
        var joined = (await ReadJsonAsync(join)).GetProperty("data");
        joined.GetProperty("id").GetString().Should().Be(groupId);
        joined.GetProperty("my_role").GetString().Should().Be("member");
        joined.GetProperty("my_points").GetInt32().Should().Be(100);
        joined.GetProperty("member_count").GetInt32().Should().Be(2);

        var again = await friend.PostAsJsonAsync("/api/v1/groups/join", new Dictionary<string, object?> { ["code"] = inviteCode });
        again.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await ReadJsonAsync(again)).GetProperty("code").GetString().Should().Be("ALREADY_MEMBER");

        var unknownCode = await outsider.PostAsJsonAsync("/api/v1/groups/join", new Dictionary<string, object?> { ["code"] = "000000" });
        unknownCode.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var badCode = await outsider.PostAsJsonAsync("/api/v1/groups/join", new Dictionary<string, object?> { ["code"] = "ABC" });
        badCode.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);

        var list = await friend.GetAsync("/api/v1/groups");
        list.StatusCode.Should().Be(HttpStatusCode.OK);
        var listJson = await ReadJsonAsync(list);
        listJson.GetProperty("data").GetArrayLength().Should().Be(1);
        listJson.GetProperty("meta").GetProperty("has_more").GetBoolean().Should().BeFalse();

        var filtered = await ReadJsonAsync(await friend.GetAsync("/api/v1/groups?q=noexiste"));
        filtered.GetProperty("data").GetArrayLength().Should().Be(0);
        var byName = await ReadJsonAsync(await friend.GetAsync("/api/v1/groups?q=compas"));
        byName.GetProperty("data").GetArrayLength().Should().Be(1);

        var detail = await friend.GetAsync($"/api/v1/groups/{groupId}");
        detail.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ReadJsonAsync(detail)).GetProperty("data").GetProperty("settings").GetProperty("bond_options").GetArrayLength().Should().Be(3);

        (await outsider.GetAsync($"/api/v1/groups/{groupId}")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await outsider.GetAsync($"/api/v1/groups/{ObjectId.GenerateNewId()}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await outsider.GetAsync($"/api/v1/groups/{groupId}/members")).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var members = await ReadJsonAsync(await owner.GetAsync($"/api/v1/groups/{groupId}/members"));
        members.GetProperty("data").GetArrayLength().Should().Be(2);
        members.GetProperty("data")[0].GetProperty("role").GetString().Should().Be("admin");

        var prefix = ownerUser.Nickname[..4];
        var byPrefix = await ReadJsonAsync(await friend.GetAsync($"/api/v1/groups/{groupId}/members?q={prefix}"));
        byPrefix.GetProperty("data").EnumerateArray()
            .Should().Contain(x => x.GetProperty("user_id").GetString() == ownerUser.Id);

        var page1 = await ReadJsonAsync(await owner.GetAsync($"/api/v1/groups/{groupId}/members?limit=1"));
        page1.GetProperty("data").GetArrayLength().Should().Be(1);
        page1.GetProperty("meta").GetProperty("has_more").GetBoolean().Should().BeTrue();
        var cursor = page1.GetProperty("meta").GetProperty("next_cursor").GetString();
        var page2 = await ReadJsonAsync(await owner.GetAsync($"/api/v1/groups/{groupId}/members?limit=1&cursor={cursor}"));
        page2.GetProperty("data").GetArrayLength().Should().Be(1);
        page2.GetProperty("meta").GetProperty("has_more").GetBoolean().Should().BeFalse();

        (await owner.GetAsync("/api/v1/groups?limit=0")).StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task Join_FullGroup_Returns409()
    {
        var (owner, _) = await NewUserAsync();
        var (latecomer, _) = await NewUserAsync();
        var (groupId, inviteCode) = await CreateGroupAsync(owner, "Lleno");
        var group = await _factory.Groups.GetByIdAsync(ObjectId.Parse(groupId), CancellationToken.None);
        group!.MemberCount = 20;

        var response = await latecomer.PostAsJsonAsync("/api/v1/groups/join", new Dictionary<string, object?> { ["code"] = inviteCode });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await ReadJsonAsync(response)).GetProperty("code").GetString().Should().Be("GROUP_FULL");
    }

    [Fact]
    public async Task Contracts_BoardDetailAcceptAndReject()
    {
        var (owner, _) = await NewUserAsync();
        var (friend, friendUser) = await NewUserAsync();
        var (outsider, _) = await NewUserAsync();
        var (groupId, inviteCode) = await CreateGroupAsync(owner, "Retadores");
        (await friend.PostAsJsonAsync("/api/v1/groups/join", new Dictionary<string, object?> { ["code"] = inviteCode }))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        var contractId = await CreateContractAsync(owner, groupId, 50, "group", null);

        var detail = await friend.GetAsync($"/api/v1/contracts/{contractId}");
        detail.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ReadJsonAsync(detail)).GetProperty("data").GetProperty("status").GetString().Should().Be("open");
        (await outsider.GetAsync($"/api/v1/contracts/{contractId}")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await friend.GetAsync($"/api/v1/contracts/{ObjectId.GenerateNewId()}")).StatusCode.Should().Be(HttpStatusCode.NotFound);

        var board = await ReadJsonAsync(await friend.GetAsync($"/api/v1/groups/{groupId}/contracts"));
        board.GetProperty("data").GetArrayLength().Should().Be(1);
        (await ReadJsonAsync(await friend.GetAsync($"/api/v1/groups/{groupId}/contracts?mine=true"))).GetProperty("data").GetArrayLength().Should().Be(0);
        (await ReadJsonAsync(await friend.GetAsync($"/api/v1/groups/{groupId}/contracts?status=open&game=valorant"))).GetProperty("data").GetArrayLength().Should().Be(1);
        (await ReadJsonAsync(await friend.GetAsync($"/api/v1/groups/{groupId}/contracts?q=seguidas"))).GetProperty("data").GetArrayLength().Should().Be(1);
        (await friend.GetAsync($"/api/v1/groups/{groupId}/contracts?status=foo")).StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await outsider.GetAsync($"/api/v1/groups/{groupId}/contracts")).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var own = await owner.PostAsync($"/api/v1/contracts/{contractId}/accept", null);
        own.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await ReadJsonAsync(own)).GetProperty("code").GetString().Should().Be("CANNOT_ACCEPT_OWN_CONTRACT");

        var accept = await friend.PostAsync($"/api/v1/contracts/{contractId}/accept", null);
        accept.StatusCode.Should().Be(HttpStatusCode.OK);
        var accepted = (await ReadJsonAsync(accept)).GetProperty("data");
        accepted.GetProperty("status").GetString().Should().Be("in_progress");
        accepted.GetProperty("accepted_by").GetString().Should().Be(friendUser.Id);
        accepted.GetProperty("my_points").GetInt32().Should().Be(50);
        _factory.Contracts.Ledger.Should().Contain(x =>
            x.RefId == ObjectId.Parse(contractId) && x.Type == "bond_lock" && x.Amount == -50 && x.BalanceAfter == 50);

        var twice = await friend.PostAsync($"/api/v1/contracts/{contractId}/accept", null);
        twice.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await ReadJsonAsync(twice)).GetProperty("code").GetString().Should().Be("INVALID_CONTRACT_STATE");

        (await ReadJsonAsync(await friend.GetAsync($"/api/v1/groups/{groupId}/contracts?status=in_progress"))).GetProperty("data").GetArrayLength().Should().Be(1);
        (await ReadJsonAsync(await friend.GetAsync($"/api/v1/groups/{groupId}/contracts?mine=true"))).GetProperty("data").GetArrayLength().Should().Be(1);
        (await ReadJsonAsync(await friend.GetAsync($"/api/v1/groups/{groupId}"))).GetProperty("data").GetProperty("my_points").GetInt32().Should().Be(50);

        var expensive = await CreateContractAsync(owner, groupId, 100, "group", null);
        var broke = await friend.PostAsync($"/api/v1/contracts/{expensive}/accept", null);
        broke.StatusCode.Should().Be(HttpStatusCode.PaymentRequired);
        (await ReadJsonAsync(broke)).GetProperty("code").GetString().Should().Be("INSUFFICIENT_POINTS");
        (await ReadJsonAsync(await friend.GetAsync($"/api/v1/groups/{groupId}"))).GetProperty("data").GetProperty("my_points").GetInt32().Should().Be(50);

        var directed = await CreateContractAsync(owner, groupId, 25, "member", friendUser.Id);
        var notTarget = await owner.PostAsync($"/api/v1/contracts/{directed}/reject", null);
        notTarget.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await ReadJsonAsync(notTarget)).GetProperty("code").GetString().Should().Be("NOT_CONTRACT_TARGET");

        var stale = await friend.PostAsJsonAsync($"/api/v1/contracts/{directed}/reject", new Dictionary<string, object?> { ["version"] = 99 });
        stale.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var reject = await friend.PostAsJsonAsync($"/api/v1/contracts/{directed}/reject", new Dictionary<string, object?> { ["version"] = 1 });
        reject.StatusCode.Should().Be(HttpStatusCode.OK);
        var rejected = (await ReadJsonAsync(reject)).GetProperty("data");
        rejected.GetProperty("status").GetString().Should().Be("cancelled");
        rejected.GetProperty("closed_reason").GetString().Should().Be("rejected");

        (await friend.PostAsync($"/api/v1/contracts/{directed}/accept", null)).StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Accept_DirectedToAnotherMember_Returns403()
    {
        var (owner, _) = await NewUserAsync();
        var (target, targetUser) = await NewUserAsync();
        var (bystander, _) = await NewUserAsync();
        var (groupId, inviteCode) = await CreateGroupAsync(owner, "Dirigidos");
        foreach (var client in new[] { target, bystander })
        {
            (await client.PostAsJsonAsync("/api/v1/groups/join", new Dictionary<string, object?> { ["code"] = inviteCode }))
                .StatusCode.Should().Be(HttpStatusCode.OK);
        }

        var contractId = await CreateContractAsync(owner, groupId, 25, "member", targetUser.Id);

        var response = await bystander.PostAsync($"/api/v1/contracts/{contractId}/accept", null);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await ReadJsonAsync(response)).GetProperty("code").GetString().Should().Be("NOT_CONTRACT_TARGET");
        (await target.PostAsync($"/api/v1/contracts/{contractId}/accept", null)).StatusCode.Should().Be(HttpStatusCode.OK);
    }

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

    private static async Task<(string Id, string InviteCode)> CreateGroupAsync(HttpClient client, string name)
    {
        var response = await client.PostAsJsonAsync("/api/v1/groups", new Dictionary<string, object?> { ["name"] = name });
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var data = (await ReadJsonAsync(response)).GetProperty("data");
        return (data.GetProperty("id").GetString()!, data.GetProperty("invite_code").GetString()!);
    }

    private static async Task<string> CreateContractAsync(HttpClient client, string groupId, int bond, string targetType, string? targetId)
    {
        var response = await client.PostAsJsonAsync($"/api/v1/groups/{groupId}/contracts", new Dictionary<string, object?>
        {
            ["game"] = "Valorant",
            ["condition"] = "Ganar tres partidas seguidas",
            ["reward"] = "Elegir el próximo juego",
            ["deadline"] = DateTimeOffset.UtcNow.AddDays(7).ToString("O"),
            ["bond_required"] = bond,
            ["target_type"] = targetType,
            ["target_id"] = targetId,
            ["source"] = "manual"
        });
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await ReadJsonAsync(response)).GetProperty("data").GetProperty("id").GetString()!;
    }

    private static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response)
    {
        var stream = await response.Content.ReadAsStreamAsync();
        using var document = await JsonDocument.ParseAsync(stream);
        return document.RootElement.Clone();
    }
}
