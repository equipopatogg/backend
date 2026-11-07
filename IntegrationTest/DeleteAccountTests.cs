using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using MongoDB.Bson;
using Xunit;

namespace IntegrationTest;

/// <summary>USR-04 (USR04-AC1..AC4).</summary>
public sealed class DeleteAccountTests : IClassFixture<PactoggWebFactory>
{
    private readonly PactoggWebFactory _factory;

    public DeleteAccountTests(PactoggWebFactory factory)
    {
        _factory = factory;
    }

    private static HttpRequestMessage Delete(object body) =>
        new(HttpMethod.Delete, "/api/v1/users/me") { Content = JsonContent.Create(body) };

    [Fact]
    public async Task WrongPasswordOrMissingConfirmation_Is422()
    {
        var (client, _) = await Api.RegisterAsync(_factory);
        (await client.SendAsync(Delete(new { password = "otra frase cualquiera", confirm = "ELIMINAR" }))).StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await client.SendAsync(Delete(new { password = "frase larga de ejemplo", confirm = "si" }))).StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task Delete_AnonymizesRevokesAndHandsOverOwnedGroup()
    {
        var (owner, ownerUser) = await Api.RegisterAsync(_factory);
        var (friend, friendUser) = await Api.RegisterAsync(_factory);
        var (groupId, code) = await Api.CreateGroupAsync(owner);
        await Api.JoinAsync(friend, code);
        _factory.PadMembers(groupId, 1);
        var contractId = await Api.CreateContractAsync(friend, groupId, ownerUser.Id);
        (await owner.PostAsync($"/api/v1/contracts/{contractId}/accept", null)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await owner.PutAsJsonAsync("/api/v1/devices/me", new Dictionary<string, object?> { ["fcm_token"] = $"token-borrado-{Guid.NewGuid():N}", ["platform"] = "android" }))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        (await owner.SendAsync(Delete(new { password = "frase larga de ejemplo", confirm = "ELIMINAR" }))).StatusCode.Should().Be(HttpStatusCode.NoContent);

        var ownerId = ObjectId.Parse(ownerUser.Id);
        var stored = await _factory.Users.GetByIdAsync(ownerId, CancellationToken.None);
        stored!.Nickname.Should().Be("Usuario eliminado");
        stored.Email.Should().NotBe(ownerUser.Email);
        stored.PasswordHash.Should().BeEmpty();
        (await _factory.Devices.ListByUsersAsync([ownerId], CancellationToken.None)).Should().BeEmpty();

        (await owner.GetAsync("/api/v1/users/me")).StatusCode.Should().Be(HttpStatusCode.Unauthorized, "el access token actual quedó revocado");
        (await Api.LoginAsync(_factory.CreateClient(), ownerUser.Email, "frase larga de ejemplo")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        (await Api.Data(await friend.GetAsync($"/api/v1/contracts/{contractId}"))).GetProperty("status").GetString().Should().Be("cancelled");
        var group = await Api.Data(await friend.GetAsync($"/api/v1/groups/{groupId}"));
        group.GetProperty("owner_id").GetString().Should().Be(friendUser.Id, "el grupo pasa al miembro más antiguo");
        group.GetProperty("my_role").GetString().Should().Be("admin");
    }

    [Fact]
    public async Task SoleMember_GroupIsDeleted()
    {
        var (client, _) = await Api.RegisterAsync(_factory);
        var (groupId, _) = await Api.CreateGroupAsync(client);
        (await client.SendAsync(Delete(new { password = "frase larga de ejemplo", confirm = "ELIMINAR" }))).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await _factory.Groups.GetByIdAsync(ObjectId.Parse(groupId), CancellationToken.None)).Should().BeNull();
    }
}
