using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using MongoDB.Bson;
using Xunit;

namespace IntegrationTest;

/// <summary>REP-01..06 (REP-AC1..AC5).</summary>
public sealed class ReportsAndBlocksTests : IClassFixture<PactoggWebFactory>
{
    private readonly PactoggWebFactory _factory;

    public ReportsAndBlocksTests(PactoggWebFactory factory)
    {
        _factory = factory;
    }

    private async Task<(HttpClient Owner, HttpClient Friend, ApiUser FriendUser, string GroupId, string EvidenceId)> GroupWithEvidenceAsync()
    {
        var (owner, _) = await Api.RegisterAsync(_factory);
        var (friend, friendUser) = await Api.RegisterAsync(_factory);
        var (groupId, code) = await Api.CreateGroupAsync(owner);
        await Api.JoinAsync(friend, code);
        _factory.PadMembers(groupId, 1);
        var contractId = await Api.CreateContractAsync(owner, groupId, friendUser.Id);
        (await friend.PostAsync($"/api/v1/contracts/{contractId}/accept", null)).StatusCode.Should().Be(HttpStatusCode.OK);
        var evidence = await friend.PostAsJsonAsync($"/api/v1/contracts/{contractId}/evidence", new Dictionary<string, object?> { ["type"] = "link", ["url"] = "https://youtu.be/dQw4w9WgXcQ" });
        evidence.StatusCode.Should().Be(HttpStatusCode.Created);
        return (owner, friend, friendUser, groupId, (await Api.Data(evidence)).GetProperty("id").GetString()!);
    }

    private static Dictionary<string, object?> ReportBody(string groupId, string type, string id) => new()
    {
        ["group_id"] = groupId, ["target_type"] = type, ["target_id"] = id, ["reason"] = "ofensivo"
    };

    [Fact]
    public async Task Report_ListAndResolveByRemovingContent()
    {
        var (owner, friend, _, groupId, evidenceId) = await GroupWithEvidenceAsync();

        var created = await owner.PostAsJsonAsync("/api/v1/reports", ReportBody(groupId, "evidence", evidenceId));
        created.StatusCode.Should().Be(HttpStatusCode.Created);
        var reportId = (await Api.Data(created)).GetProperty("id").GetString();
        (await owner.PostAsJsonAsync("/api/v1/reports", ReportBody(groupId, "evidence", evidenceId))).StatusCode.Should().Be(HttpStatusCode.Conflict);

        (await friend.GetAsync($"/api/v1/groups/{groupId}/reports")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var list = await Api.Data(await owner.GetAsync($"/api/v1/groups/{groupId}/reports?status=open"));
        list.GetArrayLength().Should().Be(1);

        var resolved = await owner.PostAsJsonAsync($"/api/v1/reports/{reportId}/resolve", new Dictionary<string, object?> { ["action"] = "remove_content" });
        resolved.StatusCode.Should().Be(HttpStatusCode.OK);
        (await Api.Data(resolved)).GetProperty("status").GetString().Should().Be("content_removed");
        (await _factory.Evidence.GetByIdAsync(ObjectId.Parse(evidenceId), CancellationToken.None))!.Moderation.Status.Should().Be("removed");
        (await owner.PostAsJsonAsync($"/api/v1/reports/{reportId}/resolve", new Dictionary<string, object?> { ["action"] = "dismiss" })).StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Report_TargetFromAnotherGroup_Is404_AndUserReportsAreEscalated()
    {
        var (owner, _, friendUser, groupId, evidenceId) = await GroupWithEvidenceAsync();
        var (stranger, _) = await Api.RegisterAsync(_factory);
        var (otherGroup, _) = await Api.CreateGroupAsync(stranger);
        (await stranger.PostAsJsonAsync("/api/v1/reports", ReportBody(otherGroup, "evidence", evidenceId))).StatusCode.Should().Be(HttpStatusCode.NotFound);

        var user = await owner.PostAsJsonAsync("/api/v1/reports", ReportBody(groupId, "user", friendUser.Id));
        user.StatusCode.Should().Be(HttpStatusCode.Created);
        (await Api.Data(user)).GetProperty("status").GetString().Should().Be("escalated");
    }

    [Fact]
    public async Task AdminRemovesContentDirectly_MemberCannot()
    {
        var (owner, friend, _, groupId, evidenceId) = await GroupWithEvidenceAsync();
        (await friend.DeleteAsync($"/api/v1/groups/{groupId}/content/evidence/{evidenceId}")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await owner.DeleteAsync($"/api/v1/groups/{groupId}/content/evidence/{evidenceId}")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await owner.DeleteAsync($"/api/v1/groups/{groupId}/content/video/{evidenceId}")).StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task Block_StopsDirectedChallenges_UntilUnblocked()
    {
        var (owner, ownerUser) = await Api.RegisterAsync(_factory);
        var (friend, friendUser) = await Api.RegisterAsync(_factory);
        var (groupId, code) = await Api.CreateGroupAsync(owner);
        await Api.JoinAsync(friend, code);
        _factory.PadMembers(groupId, 1);

        (await friend.PostAsync($"/api/v1/users/{ownerUser.Id}/block", null)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        var blocked = await owner.PostAsJsonAsync($"/api/v1/groups/{groupId}/contracts", new Dictionary<string, object?>
        {
            ["game"] = "Valorant", ["condition"] = "Ganar tres", ["reward"] = "Unos tacos", ["deadline"] = DateTimeOffset.UtcNow.AddDays(2).ToString("O"),
            ["bond_required"] = 25, ["target_type"] = "member", ["target_id"] = friendUser.Id, ["source"] = "manual"
        });
        blocked.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await Api.Code(blocked)).Should().Be("BLOCKED_BY_USER");

        (await friend.DeleteAsync($"/api/v1/users/{ownerUser.Id}/block")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        await Api.CreateContractAsync(owner, groupId, friendUser.Id);
        (await friend.PostAsync($"/api/v1/users/{friendUser.Id}/block", null)).StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }
}
