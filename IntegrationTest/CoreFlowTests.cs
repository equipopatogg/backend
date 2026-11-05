using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using IntegrationTest.Fakes;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using MongoDB.Bson;
using Pactogg.Api.Infrastructure.Audit;
using Pactogg.Api.Infrastructure.Moderation;
using Pactogg.Api.Infrastructure.Mongo;
using Pactogg.Api.Infrastructure.Storage;
using Pactogg.Api.Modules.Auth.Abstractions;
using Pactogg.Api.Modules.Contracts.Abstractions;
using Pactogg.Api.Modules.Evidence.Abstractions;
using Pactogg.Api.Modules.Groups.Abstractions;
using Pactogg.Api.Modules.Tournaments.Abstractions;
using Pactogg.Api.Modules.Tournaments.Domain;

namespace IntegrationTest;

public sealed class PactoggWebFactory : WebApplicationFactory<Program>
{
    public InMemoryUserRepository Users { get; } = new();
    public InMemoryRefreshTokenRepository RefreshTokens { get; } = new();
    public InMemoryGroupRepository Groups { get; } = new();
    public InMemoryGroupMemberRepository Members { get; } = new();
    public InMemoryContractRepository Contracts { get; }
    public InMemoryEvidenceRepository Evidence { get; } = new();
    public InMemoryTournamentRepository Tournaments { get; } = new();
    public InMemoryMatchRepository Matches { get; } = new();
    public FakeObjectStorage Storage { get; } = new();
    public FakeModerationService Moderation { get; } = new();
    public InMemoryCommitmentRepository Commitments { get; } = new();
    public InMemoryPointsLedgerRepository Ledger { get; } = new();
    public InMemoryVoteRepository Votes { get; } = new();
    public InMemoryNotificationRepository Notifications { get; } = new();

    public PactoggWebFactory()
    {
        Contracts = new InMemoryContractRepository(Members, Users, Commitments, Ledger);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["RateLimit:RegisterPermitLimit"] = "100",
                ["RateLimit:EvidencePermitLimit"] = "100"
            });
        });
        builder.ConfigureTestServices(services =>
        {
            var hosted = services.SingleOrDefault(descriptor =>
                descriptor.ServiceType == typeof(IHostedService)
                && descriptor.ImplementationType == typeof(MongoIndexInitializer));
            if (hosted is not null)
            {
                services.Remove(hosted);
            }

            services.RemoveAll<IUserRepository>();
            services.RemoveAll<IRefreshTokenRepository>();
            services.RemoveAll<IGroupRepository>();
            services.RemoveAll<IGroupMemberRepository>();
            services.RemoveAll<IContractRepository>();
            services.RemoveAll<IEvidenceRepository>();
            services.RemoveAll<ITournamentRepository>();
            services.RemoveAll<IMatchRepository>();
            services.RemoveAll<IAuditService>();
            services.RemoveAll<IModerationService>();
            services.RemoveAll<IObjectStorage>();
            services.RemoveAll<Pactogg.Api.Modules.Votes.Abstractions.IVoteRepository>();
            services.RemoveAll<Pactogg.Api.Modules.Commitments.Abstractions.ICommitmentRepository>();
            services.RemoveAll<Pactogg.Api.Modules.Notifications.Abstractions.INotificationRepository>();
            services.RemoveAll<Pactogg.Api.Modules.Points.Abstractions.IPointsLedgerRepository>();

            services.AddSingleton<IUserRepository>(Users);
            services.AddSingleton<IRefreshTokenRepository>(RefreshTokens);
            services.AddSingleton<IGroupRepository>(Groups);
            services.AddSingleton<IGroupMemberRepository>(Members);
            services.AddSingleton<IContractRepository>(Contracts);
            services.AddSingleton<IEvidenceRepository>(Evidence);
            services.AddSingleton<ITournamentRepository>(Tournaments);
            services.AddSingleton<IMatchRepository>(Matches);
            services.AddSingleton<IAuditService, FakeAuditService>();
            services.AddSingleton<IModerationService>(Moderation);
            services.AddSingleton<IObjectStorage>(Storage);
            services.RemoveAll<Pactogg.Api.Infrastructure.Security.IGoogleTokenValidator>();
            services.AddSingleton<Pactogg.Api.Infrastructure.Security.IGoogleTokenValidator, FakeGoogleTokenValidator>();
            services.AddSingleton<Pactogg.Api.Modules.Votes.Abstractions.IVoteRepository>(Votes);
            services.AddSingleton<Pactogg.Api.Modules.Commitments.Abstractions.ICommitmentRepository>(Commitments);
            services.AddSingleton<Pactogg.Api.Modules.Notifications.Abstractions.INotificationRepository>(Notifications);
            services.AddSingleton<Pactogg.Api.Modules.Points.Abstractions.IPointsLedgerRepository>(Ledger);
        });
    }
}

public sealed class CoreFlowTests : IClassFixture<PactoggWebFactory>
{
    private readonly PactoggWebFactory _factory;

    public CoreFlowTests(PactoggWebFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task RegisterGroupContractEvidenceTournamentsAndLogout_WorksEndToEnd()
    {
        var client = _factory.CreateClient();
        var email = $"user-{Guid.NewGuid():N}@example.com";
        var nickname = $"user{Guid.NewGuid():N}"[..20];

        var registerBody = new Dictionary<string, object?>
        {
            ["email"] = email,
            ["password"] = "frase larga de ejemplo",
            ["nickname"] = nickname,
            ["accept_privacy"] = true,
            ["privacy_version"] = "2026-10-01",
            ["device_id"] = "integration-test"
        };
        var register = await client.PostAsJsonAsync("/api/v1/auth/register", registerBody);
        register.StatusCode.Should().Be(System.Net.HttpStatusCode.Created);
        var registerJson = await ReadJsonAsync(register);
        var accessToken = registerJson.GetProperty("data").GetProperty("access_token").GetString();
        accessToken.Should().NotBeNullOrWhiteSpace();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var groupBody = new Dictionary<string, object?>
        {
            ["name"] = "Los Compas",
            ["settings"] = new Dictionary<string, object?>
            {
                ["jurors"] = 3,
                ["consensus"] = 0.66,
                ["bond_options"] = new[] { 25, 50, 100 }
            }
        };
        var group = await client.PostAsJsonAsync("/api/v1/groups", groupBody);
        group.StatusCode.Should().Be(System.Net.HttpStatusCode.Created);
        var groupJson = await ReadJsonAsync(group);
        var groupId = groupJson.GetProperty("data").GetProperty("id").GetString();
        groupId.Should().NotBeNullOrWhiteSpace();

        var tournamentId = ObjectId.GenerateNewId();
        _factory.Tournaments.Add(new Tournament
        {
            Id = tournamentId,
            GroupId = ObjectId.Parse(groupId),
            OrganizerId = ObjectId.Parse(registerJson.GetProperty("data").GetProperty("user").GetProperty("id").GetString()),
            Game = "Valorant",
            Format = "single_elimination",
            EntryPoints = 50,
            PrizePool = 200,
            ScheduledAt = DateTime.UtcNow.AddDays(1),
            Participants = [],
            Status = "open",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        _factory.Matches.Add(new Match
        {
            Id = ObjectId.GenerateNewId(),
            TournamentId = tournamentId,
            Round = 1,
            Position = 1,
            Status = "pending",
            UpdatedAt = DateTime.UtcNow
        });

        var contractBody = new Dictionary<string, object?>
        {
            ["group_id"] = groupId,
            ["game"] = "Valorant",
            ["condition"] = "Ganar tres partidas seguidas en ranked",
            ["reward"] = "Elegir el próximo juego del grupo",
            ["deadline"] = DateTimeOffset.UtcNow.AddDays(7).ToString("O"),
            ["bond_required"] = 50,
            ["target_type"] = "group",
            ["target_id"] = null,
            ["source"] = "manual"
        };
        var contract = await client.PostAsJsonAsync("/api/v1/contracts", contractBody);
        contract.StatusCode.Should().Be(System.Net.HttpStatusCode.Created);
        var contractJson = await ReadJsonAsync(contract);
        var contractId = contractJson.GetProperty("data").GetProperty("id").GetString();

        var evidenceBody = new Dictionary<string, object?>
        {
            ["type"] = "link",
            ["url"] = "https://www.youtube.com/watch?v=abc123"
        };
        var evidence = await client.PostAsJsonAsync($"/api/v1/contracts/{contractId}/evidence", evidenceBody);
        evidence.StatusCode.Should().Be(System.Net.HttpStatusCode.Created);
        var evidenceJson = await ReadJsonAsync(evidence);
        evidenceJson.GetProperty("data").GetProperty("moderation").GetProperty("status").GetString().Should().Be("approved");

        var tournaments = await client.GetAsync("/api/v1/tournaments");
        tournaments.StatusCode.Should().Be(System.Net.HttpStatusCode.OK);
        var tournamentsJson = await ReadJsonAsync(tournaments);
        tournamentsJson.GetProperty("data").GetArrayLength().Should().Be(1);
        tournamentsJson.GetProperty("data")[0].GetProperty("bracket").GetProperty("matches").GetArrayLength().Should().Be(1);

        var logout = await client.PostAsJsonAsync("/api/v1/auth/logout", new Dictionary<string, object?> { ["all"] = false });
        logout.StatusCode.Should().Be(System.Net.HttpStatusCode.NoContent);

        var afterLogout = await client.GetAsync("/api/v1/tournaments");
        afterLogout.StatusCode.Should().Be(System.Net.HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Evidence_RejectsDangerousNoSqlKeys()
    {
        var client = _factory.CreateClient();
        var body = new Dictionary<string, object?>
        {
            ["email"] = "user@example.com",
            ["password"] = "frase larga de ejemplo",
            ["$where"] = "1"
        };

        var response = await client.PostAsJsonAsync("/api/v1/auth/login", body);

        response.StatusCode.Should().Be(System.Net.HttpStatusCode.BadRequest);
        var json = await ReadJsonAsync(response);
        json.GetProperty("code").GetString().Should().Be("INVALID_INPUT");
    }


    [Fact]
    public async Task Contract_FromNonMember_ReturnsForbidden()
    {
        var owner = _factory.CreateClient();
        var ownerToken = await RegisterAsync(owner);
        owner.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ownerToken);

        var group = await owner.PostAsJsonAsync("/api/v1/groups", new Dictionary<string, object?> { ["name"] = "Privado" });
        group.StatusCode.Should().Be(System.Net.HttpStatusCode.Created);
        var groupJson = await ReadJsonAsync(group);
        var groupId = groupJson.GetProperty("data").GetProperty("id").GetString();

        var outsider = _factory.CreateClient();
        var outsiderToken = await RegisterAsync(outsider);
        outsider.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", outsiderToken);

        var response = await outsider.PostAsJsonAsync("/api/v1/contracts", new Dictionary<string, object?>
        {
            ["group_id"] = groupId,
            ["game"] = "Valorant",
            ["condition"] = "Reto ajeno",
            ["reward"] = "Nada",
            ["deadline"] = DateTimeOffset.UtcNow.AddDays(1).ToString("O"),
            ["bond_required"] = 50,
            ["target_type"] = "group",
            ["source"] = "manual"
        });

        response.StatusCode.Should().Be(System.Net.HttpStatusCode.Forbidden);
        var json = await ReadJsonAsync(response);
        json.GetProperty("code").GetString().Should().Be("NOT_GROUP_MEMBER");
    }


    [Fact]
    public async Task NestedContractRoute_CreatesContractAndRejectsMismatchedGroup()
    {
        var client = _factory.CreateClient();
        var token = await RegisterAsync(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var groupId = await CreateGroupAsync(client);

        var body = ContractBody(null);
        var response = await client.PostAsJsonAsync($"/api/v1/groups/{groupId}/contracts", body);
        response.StatusCode.Should().Be(System.Net.HttpStatusCode.Created);
        var json = await ReadJsonAsync(response);
        json.GetProperty("data").GetProperty("group_id").GetString().Should().Be(groupId);

        var mismatch = ContractBody(ObjectId.GenerateNewId().ToString());
        var rejected = await client.PostAsJsonAsync($"/api/v1/groups/{groupId}/contracts", mismatch);
        rejected.StatusCode.Should().Be(System.Net.HttpStatusCode.UnprocessableEntity);
        (await ReadJsonAsync(rejected)).GetProperty("code").GetString().Should().Be("VALIDATION_ERROR");
    }

    [Fact]
    public async Task NestedTournamentsRoute_ReturnsOnlyRequestedGroup()
    {
        var client = _factory.CreateClient();
        var token = await RegisterAsync(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var groupId = await CreateGroupAsync(client);

        _factory.Tournaments.Add(new Tournament
        {
            Id = ObjectId.GenerateNewId(),
            GroupId = ObjectId.Parse(groupId),
            OrganizerId = ObjectId.GenerateNewId(),
            Game = "Valorant",
            Format = "single_elimination",
            EntryPoints = 0,
            PrizePool = 100,
            ScheduledAt = DateTime.UtcNow.AddDays(2),
            Participants = [],
            Status = "open",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });

        var response = await client.GetAsync($"/api/v1/groups/{groupId}/tournaments");
        response.StatusCode.Should().Be(System.Net.HttpStatusCode.OK);
        var json = await ReadJsonAsync(response);
        json.GetProperty("data").GetArrayLength().Should().Be(1);
        json.GetProperty("data")[0].GetProperty("group_id").GetString().Should().Be(groupId);
    }

    [Fact]
    public async Task Evidence_RejectsNonHttpsAndUnknownHost()
    {
        var client = _factory.CreateClient();
        var token = await RegisterAsync(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var groupId = await CreateGroupAsync(client);
        var contractId = await CreateContractAsync(client, groupId);

        foreach (var url in new[] { "http://youtube.com/watch?v=1", "https://evil.example/video", "https://user:pass@youtube.com/watch?v=1", "https://youtube.com:8443/watch?v=1" })
        {
            var response = await client.PostAsJsonAsync($"/api/v1/contracts/{contractId}/evidence", new Dictionary<string, object?>
            {
                ["type"] = "link",
                ["url"] = url
            });

            response.StatusCode.Should().Be(System.Net.HttpStatusCode.UnprocessableEntity);
            (await ReadJsonAsync(response)).GetProperty("code").GetString().Should().Be("INVALID_EVIDENCE_URL");
        }
    }

    [Fact]
    public async Task Evidence_RejectsInvalidImageAndCapsAtThreeLinks()
    {
        var client = _factory.CreateClient();
        var token = await RegisterAsync(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var groupId = await CreateGroupAsync(client);
        var contractId = await CreateContractAsync(client, groupId);

        using (var form = new MultipartFormDataContent())
        {
            var file = new ByteArrayContent("not-an-image"u8.ToArray());
            file.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/png");
            form.Add(file, "file", "evidence.png");
            var photo = await client.PostAsync($"/api/v1/contracts/{contractId}/evidence", form);
            photo.StatusCode.Should().Be(System.Net.HttpStatusCode.UnprocessableEntity);
            (await ReadJsonAsync(photo)).GetProperty("code").GetString().Should().Be("UNSUPPORTED_MEDIA_TYPE");
        }

        for (var i = 0; i < 3; i++)
        {
            var link = await client.PostAsJsonAsync($"/api/v1/contracts/{contractId}/evidence", new Dictionary<string, object?>
            {
                ["type"] = "link",
                ["url"] = "https://www.youtube.com/watch?v=abc123"
            });
            link.StatusCode.Should().Be(System.Net.HttpStatusCode.Created);
        }

        var fourth = await client.PostAsJsonAsync($"/api/v1/contracts/{contractId}/evidence", new Dictionary<string, object?>
        {
            ["type"] = "link",
            ["url"] = "https://www.youtube.com/watch?v=abc123"
        });
        fourth.StatusCode.Should().Be(System.Net.HttpStatusCode.Conflict);
        (await ReadJsonAsync(fourth)).GetProperty("code").GetString().Should().Be("EVIDENCE_LIMIT_REACHED");
    }

    [Fact]
    public async Task Evidence_FirstEvidenceMovesContractToVoting()
    {
        var client = _factory.CreateClient();
        var token = await RegisterAsync(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var groupId = await CreateGroupAsync(client);
        var contractId = await CreateContractAsync(client, groupId);

        var link = await client.PostAsJsonAsync($"/api/v1/contracts/{contractId}/evidence", new Dictionary<string, object?>
        {
            ["type"] = "link",
            ["url"] = "https://www.youtube.com/watch?v=abc123"
        });
        link.StatusCode.Should().Be(System.Net.HttpStatusCode.Created);

        var stored = await _factory.Contracts.GetByIdAsync(ObjectId.Parse(contractId), CancellationToken.None);
        stored!.Status.Should().Be("voting");
        stored.EvidenceCount.Should().Be(1);
    }

    [Fact]
    public async Task Contract_TargetingYourself_Returns422()
    {
        var client = _factory.CreateClient();
        var register = await client.PostAsJsonAsync("/api/v1/auth/register", new Dictionary<string, object?>
        {
            ["email"] = $"self-{Guid.NewGuid():N}@example.com",
            ["password"] = "frase larga de ejemplo",
            ["nickname"] = $"s{Guid.NewGuid():N}"[..20],
            ["accept_privacy"] = true,
            ["privacy_version"] = "2026-10-01"
        });
        var data = (await ReadJsonAsync(register)).GetProperty("data");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", data.GetProperty("access_token").GetString());
        var userId = data.GetProperty("user").GetProperty("id").GetString();
        var groupId = await CreateGroupAsync(client);

        var body = ContractBody(groupId);
        body["target_type"] = "member";
        body["target_id"] = userId;
        var response = await client.PostAsJsonAsync("/api/v1/contracts", body);

        response.StatusCode.Should().Be(System.Net.HttpStatusCode.UnprocessableEntity);
        (await ReadJsonAsync(response)).GetProperty("code").GetString().Should().Be("INVALID_TARGET");
    }


    [Fact]
    public async Task Request_WithUnknownField_Returns400()
    {
        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new Dictionary<string, object?>
        {
            ["email"] = "user@example.com",
            ["password"] = "frase larga de ejemplo",
            ["unexpected"] = true
        });

        response.StatusCode.Should().Be(System.Net.HttpStatusCode.BadRequest);
        (await ReadJsonAsync(response)).GetProperty("code").GetString().Should().Be("INVALID_INPUT");
    }

    private static Dictionary<string, object?> ContractBody(string? groupId) => new()
    {
        ["group_id"] = groupId,
        ["game"] = "Valorant",
        ["condition"] = "Ganar tres partidas seguidas",
        ["reward"] = "Elegir el próximo juego",
        ["deadline"] = DateTimeOffset.UtcNow.AddDays(7).ToString("O"),
        ["bond_required"] = 50,
        ["target_type"] = "group",
        ["source"] = "manual"
    };

    private static async Task<string> CreateGroupAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync("/api/v1/groups", new Dictionary<string, object?>
        {
            ["name"] = $"Grupo {Guid.NewGuid():N}"[..20]
        });
        response.StatusCode.Should().Be(System.Net.HttpStatusCode.Created);
        return (await ReadJsonAsync(response)).GetProperty("data").GetProperty("id").GetString()!;
    }

    private static async Task<string> CreateContractAsync(HttpClient client, string groupId)
    {
        var response = await client.PostAsJsonAsync("/api/v1/contracts", ContractBody(groupId));
        response.StatusCode.Should().Be(System.Net.HttpStatusCode.Created);
        return (await ReadJsonAsync(response)).GetProperty("data").GetProperty("id").GetString()!;
    }

    private static async Task<string> RegisterAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync("/api/v1/auth/register", new Dictionary<string, object?>
        {
            ["email"] = $"user-{Guid.NewGuid():N}@example.com",
            ["password"] = "frase larga de ejemplo",
            ["nickname"] = $"u{Guid.NewGuid():N}"[..20],
            ["accept_privacy"] = true,
            ["privacy_version"] = "2026-10-01"
        });
        response.StatusCode.Should().Be(System.Net.HttpStatusCode.Created);
        var json = await ReadJsonAsync(response);
        return json.GetProperty("data").GetProperty("access_token").GetString()!;
    }

    private static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response)
    {
        var stream = await response.Content.ReadAsStreamAsync();
        using var document = await JsonDocument.ParseAsync(stream);
        return document.RootElement.Clone();
    }
}
