using System.Net;
using FluentAssertions;
using MongoDB.Bson;
using Pactogg.Api.Modules.Commitments.Domain;
using Xunit;

namespace IntegrationTest;

/// <summary>PTS-03, COM-02, COM-05 y ACH-03.</summary>
public sealed class DetailEndpointsTests : IClassFixture<PactoggWebFactory>
{
    private readonly PactoggWebFactory _factory;

    public DetailEndpointsTests(PactoggWebFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task PointsMovement_OnlyOwnerOrAdmin_AndRankingHasKing()
    {
        var (owner, _) = await Api.RegisterAsync(_factory);
        var (friend, friendUser) = await Api.RegisterAsync(_factory);
        var (outsider, _) = await Api.RegisterAsync(_factory);
        var (groupId, code) = await Api.CreateGroupAsync(owner);
        await Api.JoinAsync(friend, code);
        _factory.PadMembers(groupId, 1);
        var contractId = await Api.CreateContractAsync(owner, groupId, friendUser.Id);
        (await friend.PostAsync($"/api/v1/contracts/{contractId}/accept", null)).StatusCode.Should().Be(HttpStatusCode.OK);

        var movementId = _factory.Ledger.Entries.Single(x => x.RefId.ToString() == contractId && x.Type == "bond_lock").Id.ToString();
        var mine = await Api.Data(await friend.GetAsync($"/api/v1/points/movements/{movementId}"));
        mine.GetProperty("amount").GetInt32().Should().Be(-25);
        (await owner.GetAsync($"/api/v1/points/movements/{movementId}")).StatusCode.Should().Be(HttpStatusCode.OK, "el admin del grupo puede verlo");
        (await outsider.GetAsync($"/api/v1/points/movements/{movementId}")).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var ranking = await Api.Data(await owner.GetAsync($"/api/v1/groups/{groupId}/ranking"));
        ranking.GetProperty("items").GetArrayLength().Should().BeGreaterThan(1);
        // Dueño y relleno empatan con 100 puntos: con empate en el primer lugar no hay Rey del Grupo.
        (ranking.TryGetProperty("king", out var king) && king.ValueKind != System.Text.Json.JsonValueKind.Null).Should().BeFalse();
    }

    [Fact]
    public async Task CommitmentDetailAndSummary()
    {
        var (owner, ownerUser) = await Api.RegisterAsync(_factory);
        var (friend, friendUser) = await Api.RegisterAsync(_factory);
        var (groupId, code) = await Api.CreateGroupAsync(owner);
        await Api.JoinAsync(friend, code);
        var commitment = new Commitment
        {
            Id = ObjectId.GenerateNewId(),
            GroupId = ObjectId.Parse(groupId),
            ContractId = ObjectId.GenerateNewId(),
            DebtorId = ObjectId.Parse(ownerUser.Id),
            CreditorId = ObjectId.Parse(friendUser.Id),
            Description = "Unos tacos",
            Game = "Valorant",
            Status = "pending",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _factory.Commitments.Add(commitment);

        var detail = await Api.Data(await friend.GetAsync($"/api/v1/commitments/{commitment.Id}"));
        detail.GetProperty("id").GetString().Should().Be(commitment.Id.ToString());

        var ownerSummary = await Api.Data(await owner.GetAsync($"/api/v1/groups/{groupId}/commitments/summary"));
        ownerSummary.GetProperty("debo").GetInt32().Should().Be(1);
        ownerSummary.GetProperty("me_deben").GetInt32().Should().Be(0);
        var friendSummary = await Api.Data(await friend.GetAsync($"/api/v1/groups/{groupId}/commitments/summary"));
        friendSummary.GetProperty("me_deben").GetInt32().Should().Be(1);

        var (outsider, _) = await Api.RegisterAsync(_factory);
        (await outsider.GetAsync($"/api/v1/commitments/{commitment.Id}")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
