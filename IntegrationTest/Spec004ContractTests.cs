using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Xunit;

namespace IntegrationTest;

/// <summary>Spec 004 Contratos: criterios que no tenían prueba dedicada (004-AC5 a 004-AC9).</summary>
public sealed class Spec004ContractTests : IClassFixture<PactoggWebFactory>
{
    private readonly PactoggWebFactory _factory;

    public Spec004ContractTests(PactoggWebFactory factory)
    {
        _factory = factory;
    }

    private async Task<(HttpClient Owner, string GroupId, string FriendId)> GroupWithFriendAsync()
    {
        var (owner, _) = await Api.RegisterAsync(_factory);
        var (friend, friendUser) = await Api.RegisterAsync(_factory);
        var (groupId, code) = await Api.CreateGroupAsync(owner);
        await Api.JoinAsync(friend, code);
        _factory.PadMembers(groupId, 1);
        return (owner, groupId, friendUser.Id);
    }

    private static Dictionary<string, object?> Body(string game = "Valorant", string condition = "Ganar tres partidas seguidas", int bond = 25, string targetType = "group", string? targetId = null) => new()
    {
        ["game"] = game,
        ["condition"] = condition,
        ["reward"] = "Unos tacos al pastor",
        ["deadline"] = DateTimeOffset.UtcNow.AddDays(3).ToString("O"),
        ["bond_required"] = bond,
        ["target_type"] = targetType,
        ["target_id"] = targetId,
        ["source"] = "manual"
    };

    [Theory]
    [InlineData("Juego reject", "Ganar tres partidas seguidas")]
    [InlineData("Valorant", "Condición reject")]
    public async Task ModeratedText_Returns422ContentRejected(string game, string condition)
    {
        var (owner, groupId, _) = await GroupWithFriendAsync();
        var response = await owner.PostAsJsonAsync($"/api/v1/groups/{groupId}/contracts", Body(game, condition));
        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await Api.Code(response)).Should().Be("CONTENT_REJECTED");
    }

    [Fact]
    public async Task EditWithModeratedGame_Returns422()
    {
        var (owner, groupId, _) = await GroupWithFriendAsync();
        var id = await Api.CreateContractAsync(owner, groupId, null);
        var response = await owner.PatchAsJsonAsync($"/api/v1/contracts/{id}", new Dictionary<string, object?> { ["version"] = 1, ["game"] = "otro reject" });
        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await Api.Code(response)).Should().Be("CONTENT_REJECTED");
    }

    [Fact]
    public async Task BondNotAllowedByGroup_Returns422InvalidBond()
    {
        var (owner, groupId, _) = await GroupWithFriendAsync();
        var response = await owner.PostAsJsonAsync($"/api/v1/groups/{groupId}/contracts", Body(bond: 33));
        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await Api.Code(response)).Should().Be("INVALID_BOND");
    }

    [Fact]
    public async Task TargetOutsideTheGroup_Returns422InvalidTarget()
    {
        var (owner, groupId, _) = await GroupWithFriendAsync();
        var (_, stranger) = await Api.RegisterAsync(_factory);
        var response = await owner.PostAsJsonAsync($"/api/v1/groups/{groupId}/contracts", Body(targetType: "member", targetId: stranger.Id));
        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await Api.Code(response)).Should().Be("INVALID_TARGET");
    }

    [Fact]
    public async Task DirectedContract_NotifiesOnlyTheTarget()
    {
        var (owner, groupId, friendId) = await GroupWithFriendAsync();
        var id = await Api.CreateContractAsync(owner, groupId, friendId);

        var friendNotes = _factory.Notifications.For(MongoDB.Bson.ObjectId.Parse(friendId))
            .Where(n => n.Type == "contract_received" && n.ContractId.ToString() == id)
            .ToList();
        friendNotes.Should().ContainSingle();
        var others = _factory.Users.All()
            .Where(u => u.Id.ToString() != friendId)
            .SelectMany(u => _factory.Notifications.For(u.Id))
            .Where(n => n.Type == "contract_received" && n.ContractId.ToString() == id);
        others.Should().BeEmpty();
    }
}
