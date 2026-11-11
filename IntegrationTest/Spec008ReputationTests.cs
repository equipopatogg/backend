using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Bson;
using Pactogg.Api.Modules.Auth.Domain;
using Pactogg.Api.Modules.Contracts.Lifecycle;
using Pactogg.Api.Modules.Reputation;
using Xunit;

namespace IntegrationTest;

/// <summary>Spec 008: Índice de Palabra (AC9), Muro de la Vergüenza (AC7) y logros (AC8).</summary>
public sealed class Spec008ReputationTests : IClassFixture<PactoggWebFactory>
{
    private readonly PactoggWebFactory _factory;

    public Spec008ReputationTests(PactoggWebFactory factory)
    {
        _factory = factory;
    }

    [Theory]
    [InlineData(0, 0, 0, 0, 0, 0, 50)]
    [InlineData(2, 0, 1, 1, 0, 12, 65)]   // 50 + 8 − 5 + 2 + min(10, 12)
    [InlineData(25, 0, 0, 0, 0, 0, 100)]  // tope superior
    [InlineData(0, 5, 0, 0, 0, 0, 0)]     // tope inferior
    [InlineData(1, 1, 0, 0, 1, 0, 38)]    // 50 + 4 − 12 − 4
    public void AC9_WordIndexFormula_IsDeterministicAndClamped(int completed, int cheated, int expired, int onTime, int overdue, int aligned, int expected)
    {
        WordIndexCalculator.Compute(completed, cheated, expired, onTime, overdue, aligned).Should().Be(expected);
        WordIndexCalculator.Compute(new UserStats
        {
            Completed = completed,
            Cheated = cheated,
            Expired = expired,
            CommitmentsOnTime = onTime,
            CommitmentsOverdue = overdue,
            AlignedVotes = aligned
        }).Should().Be(expected);
    }

    [Fact]
    public async Task AC8_AC9_FulfilledContract_UpdatesIndex_AwardsFirstBadgeOnce_AndAchievementsNeedASharedGroup()
    {
        var (owner, _) = await Api.RegisterAsync(_factory);
        var (doer, doerUser) = await Api.RegisterAsync(_factory);
        var (juror, jurorUser) = await Api.RegisterAsync(_factory);
        var (groupId, code) = await Api.CreateGroupAsync(owner);
        await Api.JoinAsync(doer, code);
        await Api.JoinAsync(juror, code);

        for (var round = 0; round < 2; round++)
        {
            var contractId = await Api.CreateContractAsync(owner, groupId, doerUser.Id);
            (await doer.PostAsync($"/api/v1/contracts/{contractId}/accept", null)).StatusCode.Should().Be(HttpStatusCode.OK);
            (await doer.PostAsJsonAsync($"/api/v1/contracts/{contractId}/evidence", new Dictionary<string, object?>
            {
                ["type"] = "link",
                ["url"] = $"https://www.youtube.com/watch?v=indice{round}"
            })).StatusCode.Should().Be(HttpStatusCode.Created);
            (await juror.PutAsJsonAsync($"/api/v1/contracts/{contractId}/votes", new Dictionary<string, object?> { ["vote"] = "legit" }))
                .StatusCode.Should().Be(HttpStatusCode.OK);
            _factory.Contracts.Get(ObjectId.Parse(contractId)).Status.Should().Be("fulfilled");
        }

        var doerProfile = await Api.Data(await owner.GetAsync($"/api/v1/users/{doerUser.Id}"));
        doerProfile.GetProperty("stats").GetProperty("word_index").GetInt32().Should().Be(58, "50 + 4·2 cumplidos");
        var jurorProfile = await Api.Data(await owner.GetAsync($"/api/v1/users/{jurorUser.Id}"));
        jurorProfile.GetProperty("stats").GetProperty("word_index").GetInt32().Should().Be(52, "dos votos alineados con el jurado");

        var achievements = await Api.Data(await owner.GetAsync($"/api/v1/users/{doerUser.Id}/achievements"));
        achievements.EnumerateArray().Select(x => x.GetProperty("code").GetString()).Should().ContainSingle(x => x == "primer_reto");
        achievements[0].GetProperty("name").GetString().Should().NotBeNullOrWhiteSpace();
        achievements[0].GetProperty("type").GetString().Should().Be("badge");

        var (outsider, _) = await Api.RegisterAsync(_factory);
        (await outsider.GetAsync($"/api/v1/users/{doerUser.Id}/achievements")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await doer.GetAsync($"/api/v1/users/{doerUser.Id}/achievements")).StatusCode.Should().Be(HttpStatusCode.OK, "cada quien ve los suyos");
    }

    [Fact]
    public async Task AC8_Streaks_AreAwardedOncePerPerson()
    {
        var (owner, _) = await Api.RegisterAsync(_factory);
        var (doer, doerUser) = await Api.RegisterAsync(_factory);
        var (groupId, code) = await Api.CreateGroupAsync(owner);
        await Api.JoinAsync(doer, code);
        var user = (await _factory.Users.GetByIdAsync(ObjectId.Parse(doerUser.Id), CancellationToken.None))!;
        user.Stats.Completed = 5;
        user.Stats.Streak = 5;

        using var scope = _factory.Services.CreateScope();
        var reputation = scope.ServiceProvider.GetRequiredService<ReputationService>();
        for (var i = 0; i < 2; i++)
        {
            var contract = new Pactogg.Api.Modules.Contracts.Domain.Contract
            {
                Id = ObjectId.GenerateNewId(),
                GroupId = ObjectId.Parse(groupId),
                ChallengerId = ObjectId.GenerateNewId(),
                ExecutorId = user.Id,
                Status = "fulfilled"
            };
            await reputation.OnContractResolvedAsync(contract, "admin", CancellationToken.None);
        }

        _factory.Achievements.All.Where(x => x.UserId == user.Id).Select(x => x.Code)
            .Should().BeEquivalentTo(["primer_reto", "racha_3", "racha_5"]);
    }

    [Fact]
    public async Task AC9_AcceptedContractThatExpires_LowersTheIndexOfWhoAccepted()
    {
        var (owner, _) = await Api.RegisterAsync(_factory);
        var (doer, doerUser) = await Api.RegisterAsync(_factory);
        var (groupId, code) = await Api.CreateGroupAsync(owner);
        await Api.JoinAsync(doer, code);
        _factory.PadMembers(groupId, 1);
        var contractId = await Api.CreateContractAsync(owner, groupId, doerUser.Id);
        (await doer.PostAsync($"/api/v1/contracts/{contractId}/accept", null)).StatusCode.Should().Be(HttpStatusCode.OK);
        _factory.Contracts.Get(ObjectId.Parse(contractId)).Deadline = DateTime.UtcNow.AddMinutes(-1);

        using (var scope = _factory.Services.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<ContractLifecycleService>().RunOnceAsync(DateTime.UtcNow, CancellationToken.None);
        }

        _factory.Contracts.Get(ObjectId.Parse(contractId)).Status.Should().Be("expired");
        var stats = (await _factory.Users.GetByIdAsync(ObjectId.Parse(doerUser.Id), CancellationToken.None))!.Stats;
        stats.Expired.Should().Be(1);
        stats.WordIndex.Should().Be(45);
    }

    [Fact]
    public async Task AC7_ShameWall_ListsCheatingAndExpired_NotFulfilled_OnlyForMembers()
    {
        var (owner, _) = await Api.RegisterAsync(_factory);
        var (doer, doerUser) = await Api.RegisterAsync(_factory);
        var (groupId, code) = await Api.CreateGroupAsync(owner);
        await Api.JoinAsync(doer, code);
        _factory.PadMembers(groupId, 1);

        var ids = new List<string>();
        foreach (var status in new[] { "cheating", "expired", "fulfilled" })
        {
            var id = await Api.CreateContractAsync(owner, groupId, doerUser.Id);
            var contract = _factory.Contracts.Get(ObjectId.Parse(id));
            contract.Status = status;
            contract.AcceptedBy = ObjectId.Parse(doerUser.Id);
            contract.ResolvedAt = DateTime.UtcNow;
            ids.Add(id);
        }

        var response = await doer.GetAsync($"/api/v1/groups/{groupId}/shame");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var items = await Api.Data(response);
        items.EnumerateArray().Select(x => x.GetProperty("contract_id").GetString()).Should().BeEquivalentTo(new[] { ids[0], ids[1] });
        items[0].GetProperty("contract_id").GetString().Should().Be(ids[1], "más nuevos primero");
        items[0].GetProperty("label").GetString().Should().NotBeNullOrWhiteSpace();
        items[0].GetProperty("executor").GetProperty("user_id").GetString().Should().Be(doerUser.Id);
        items[0].TryGetProperty("photos", out _).Should().BeFalse("sin fotos ni comentarios");

        var (outsider, _) = await Api.RegisterAsync(_factory);
        (await outsider.GetAsync($"/api/v1/groups/{groupId}/shame")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
