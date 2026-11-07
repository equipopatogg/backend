using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using MongoDB.Bson;
using Xunit;

namespace IntegrationTest;

/// <summary>GRP-04..07 y MEM-03..06 (ADM-AC1..AC6).</summary>
public sealed class GroupAdminTests : IClassFixture<PactoggWebFactory>
{
    private readonly PactoggWebFactory _factory;

    public GroupAdminTests(PactoggWebFactory factory)
    {
        _factory = factory;
    }

    private async Task<(HttpClient Owner, ApiUser OwnerUser, HttpClient Friend, ApiUser FriendUser, string GroupId, string Code)> SetupAsync()
    {
        var (owner, ownerUser) = await Api.RegisterAsync(_factory);
        var (friend, friendUser) = await Api.RegisterAsync(_factory);
        var (groupId, code) = await Api.CreateGroupAsync(owner);
        await Api.JoinAsync(friend, code);
        _factory.PadMembers(groupId, 1);
        return (owner, ownerUser, friend, friendUser, groupId, code);
    }

    [Fact]
    public async Task Admin_EditsNameAndSettings_MemberGets403()
    {
        var (owner, _, friend, _, groupId, _) = await SetupAsync();

        var ok = await owner.PatchAsJsonAsync($"/api/v1/groups/{groupId}", new Dictionary<string, object?>
        {
            ["name"] = "Nuevo nombre",
            ["settings"] = new Dictionary<string, object?> { ["jurors"] = 5, ["consensus"] = 0.75, ["bond_options"] = new[] { 10, 20 } }
        });
        ok.StatusCode.Should().Be(HttpStatusCode.OK);
        var data = await Api.Data(ok);
        data.GetProperty("name").GetString().Should().Be("Nuevo nombre");
        data.GetProperty("settings").GetProperty("jurors").GetInt32().Should().Be(5);

        (await owner.PatchAsJsonAsync($"/api/v1/groups/{groupId}", new Dictionary<string, object?> { ["name"] = "nombre reject" }))
            .StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await owner.PatchAsJsonAsync($"/api/v1/groups/{groupId}", new Dictionary<string, object?> { ["settings"] = new Dictionary<string, object?> { ["jurors"] = 9 } }))
            .StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);

        var denied = await friend.PatchAsJsonAsync($"/api/v1/groups/{groupId}", new Dictionary<string, object?> { ["name"] = "Mío" });
        denied.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await Api.Code(denied)).Should().Be("NOT_GROUP_ADMIN");
    }

    [Fact]
    public async Task RegenerateAndDisableInviteCode()
    {
        var (owner, _, _, _, groupId, oldCode) = await SetupAsync();
        var regenerated = await Api.Data(await owner.PostAsync($"/api/v1/groups/{groupId}/invite-code/regenerate", null));
        var newCode = regenerated.GetProperty("invite_code").GetString()!;
        newCode.Should().NotBe(oldCode);

        var (late, _) = await Api.RegisterAsync(_factory);
        (await late.PostAsJsonAsync("/api/v1/groups/join", new Dictionary<string, object?> { ["code"] = oldCode })).StatusCode.Should().Be(HttpStatusCode.NotFound);

        (await owner.PatchAsJsonAsync($"/api/v1/groups/{groupId}/invite-code", new Dictionary<string, object?> { ["active"] = false })).StatusCode.Should().Be(HttpStatusCode.OK);
        (await late.PostAsJsonAsync("/api/v1/groups/join", new Dictionary<string, object?> { ["code"] = newCode })).StatusCode.Should().Be(HttpStatusCode.NotFound);

        (await owner.PatchAsJsonAsync($"/api/v1/groups/{groupId}/invite-code", new Dictionary<string, object?> { ["active"] = true })).StatusCode.Should().Be(HttpStatusCode.OK);
        await Api.JoinAsync(late, newCode);
    }

    [Fact]
    public async Task Roles_CreatorCannotBeTouched_PromotedMemberIsNotified()
    {
        var (owner, ownerUser, friend, friendUser, groupId, _) = await SetupAsync();

        var promote = await owner.PatchAsJsonAsync($"/api/v1/groups/{groupId}/members/{friendUser.Id}", new Dictionary<string, object?> { ["role"] = "admin" });
        promote.StatusCode.Should().Be(HttpStatusCode.OK);
        (await Api.Data(promote)).GetProperty("role").GetString().Should().Be("admin");
        _factory.Notifications.For(ObjectId.Parse(friendUser.Id)).Should().Contain(n => n.Type == "group_role_changed");

        var demoteOwner = await friend.PatchAsJsonAsync($"/api/v1/groups/{groupId}/members/{ownerUser.Id}", new Dictionary<string, object?> { ["role"] = "member" });
        demoteOwner.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await Api.Code(demoteOwner)).Should().Be("CANNOT_MODIFY_OWNER");
        (await friend.DeleteAsync($"/api/v1/groups/{groupId}/members/{ownerUser.Id}")).StatusCode.Should().Be(HttpStatusCode.Conflict);

        (await owner.PatchAsJsonAsync($"/api/v1/groups/{groupId}/members/{friendUser.Id}", new Dictionary<string, object?> { ["role"] = "jefe" }))
            .StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task KickedMember_ContractsCancelledBondRefunded_CanRejoinWithBalance()
    {
        var (owner, _, friend, friendUser, groupId, code) = await SetupAsync();
        var contractId = await Api.CreateContractAsync(owner, groupId, friendUser.Id);
        var accept = await friend.PostAsync($"/api/v1/contracts/{contractId}/accept", null);
        accept.StatusCode.Should().Be(HttpStatusCode.OK);
        (await Api.Data(accept)).GetProperty("my_points").GetInt32().Should().Be(75);

        (await owner.DeleteAsync($"/api/v1/groups/{groupId}/members/{friendUser.Id}")).StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await Api.Data(await owner.GetAsync($"/api/v1/contracts/{contractId}"))).GetProperty("status").GetString().Should().Be("cancelled");
        (await friend.GetAsync($"/api/v1/groups/{groupId}")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        _factory.Notifications.For(ObjectId.Parse(friendUser.Id)).Should().Contain(n => n.Type == "group_member_removed");

        var back = await friend.PostAsJsonAsync("/api/v1/groups/join", new Dictionary<string, object?> { ["code"] = code });
        back.StatusCode.Should().Be(HttpStatusCode.OK);
        (await Api.Data(back)).GetProperty("my_points").GetInt32().Should().Be(100, "la fianza se devolvió y conserva su saldo");
    }

    [Fact]
    public async Task LeaveAndMyMembership()
    {
        var (owner, _, friend, _, groupId, _) = await SetupAsync();

        var me = await Api.Data(await friend.GetAsync($"/api/v1/groups/{groupId}/members/me"));
        me.GetProperty("role").GetString().Should().Be("member");
        me.GetProperty("points_balance").GetInt32().Should().Be(100);

        var ownerLeaves = await owner.DeleteAsync($"/api/v1/groups/{groupId}/members/me");
        ownerLeaves.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await Api.Code(ownerLeaves)).Should().Be("OWNER_CANNOT_LEAVE");

        (await friend.DeleteAsync($"/api/v1/groups/{groupId}/members/me")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await friend.GetAsync($"/api/v1/groups/{groupId}/members/me")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task OwnerDeletesGroup_CancelsContractsAndFreesMembers()
    {
        var (owner, _, friend, _, groupId, _) = await SetupAsync();
        var contractId = await Api.CreateContractAsync(owner, groupId, null);

        (await friend.DeleteAsync($"/api/v1/groups/{groupId}")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await owner.DeleteAsync($"/api/v1/groups/{groupId}")).StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await owner.GetAsync($"/api/v1/groups/{groupId}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        _factory.Contracts.GetByIdAsync(ObjectId.Parse(contractId), CancellationToken.None).Result!.Status.Should().Be("cancelled");
        var mine = await Api.Data(await friend.GetAsync("/api/v1/groups"));
        mine.EnumerateArray().Select(x => x.GetProperty("id").GetString()).Should().NotContain(groupId);
    }
}
