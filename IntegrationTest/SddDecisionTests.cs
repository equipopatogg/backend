using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Pactogg.Api.Infrastructure.Security;
using Xunit;

namespace IntegrationTest;

/// <summary>Cuenta cuántas verificaciones de contraseña hace el login (Q-020).</summary>
public sealed class CountingPasswordHasher : IPasswordHasher
{
    private readonly Argon2PasswordHasher _inner = new();
    private int _verifies;

    public int Verifies => Volatile.Read(ref _verifies);

    public string Hash(string password) => _inner.Hash(password);

    public bool Verify(string hash, string password)
    {
        Interlocked.Increment(ref _verifies);
        return _inner.Verify(hash, password);
    }
}

public sealed class CountingHasherFactory : PactoggWebFactory
{
    public CountingPasswordHasher Hasher { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IPasswordHasher>();
            services.AddSingleton<IPasswordHasher>(Hasher);
        });
    }
}

/// <summary>Decisiones del PO del 2026-11-06 al validar 001–003 (Q-019, Q-020, Q-021).</summary>
public sealed class SddDecisionTests : IClassFixture<CountingHasherFactory>
{
    private readonly CountingHasherFactory _factory;

    public SddDecisionTests(CountingHasherFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Q020_LoginWithUnknownEmail_DoesTheSameHashWorkAsAWrongPassword()
    {
        var (_, user) = await Api.RegisterAsync(_factory);
        var anonymous = _factory.CreateClient();

        var before = _factory.Hasher.Verifies;
        var unknown = await Api.LoginAsync(anonymous, $"nadie-{Guid.NewGuid():N}@example.com", "frase larga de ejemplo");
        var afterUnknown = _factory.Hasher.Verifies;
        var wrong = await Api.LoginAsync(anonymous, user.Email, "otra frase equivocada");
        var afterWrong = _factory.Hasher.Verifies;

        unknown.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        wrong.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await Api.Code(unknown)).Should().Be("INVALID_CREDENTIALS");
        (await Api.Code(wrong)).Should().Be("INVALID_CREDENTIALS");
        (afterUnknown - before).Should().Be(1, "un correo inexistente también verifica un hash Argon2id");
        (afterWrong - afterUnknown).Should().Be(1);
    }

    [Fact]
    public async Task Q021_GroupNameRejectedByModeration_Returns422AndIsNotStored()
    {
        var (client, _) = await Api.RegisterAsync(_factory);
        var before = _factory.Groups.Count;

        var response = await client.PostAsJsonAsync("/api/v1/groups", Api.Group("Grupo reject"));

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await Api.Code(response)).Should().Be("CONTENT_REJECTED");
        _factory.Groups.Count.Should().Be(before);
    }

    [Theory]
    [InlineData("A", HttpStatusCode.UnprocessableEntity)]
    [InlineData("AB", HttpStatusCode.Created)]
    [InlineData("1234567890123456789012345678901234567890", HttpStatusCode.Created)]
    [InlineData("12345678901234567890123456789012345678901", HttpStatusCode.UnprocessableEntity)]
    public async Task Q019_GroupNameLengthIs2To40(string name, HttpStatusCode expected)
    {
        var (client, _) = await Api.RegisterAsync(_factory);
        (await client.PostAsJsonAsync("/api/v1/groups", Api.Group(name))).StatusCode.Should().Be(expected);
    }
    /// <summary>002 · ID 13: tras 5 fallos la cuenta queda bloqueada temporalmente (429), incluso con la contraseña correcta.</summary>
    [Fact]
    public async Task Login_AfterFiveFailures_IsTemporarilyLocked()
    {
        var (_, user) = await Api.RegisterAsync(_factory);
        var anonymous = _factory.CreateClient();
        for (var i = 0; i < 5; i++)
        {
            (await Api.LoginAsync(anonymous, user.Email, "contraseña equivocada larga")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }

        (await Api.LoginAsync(anonymous, user.Email, "frase larga de ejemplo")).StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
    }

    /// <summary>003 · ID 30: máximo 5 grupos por usuario (409 GROUP_LIMIT_REACHED).</summary>
    [Fact]
    public async Task CreateGroup_SixthGroup_Returns409()
    {
        var (client, _) = await Api.RegisterAsync(_factory);
        for (var i = 1; i <= 5; i++)
        {
            (await client.PostAsJsonAsync("/api/v1/groups", Api.Group($"Grupo {i}"))).StatusCode.Should().Be(HttpStatusCode.Created);
        }

        var sixth = await client.PostAsJsonAsync("/api/v1/groups", Api.Group("Grupo 6"));
        sixth.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await Api.Code(sixth)).Should().Be("GROUP_LIMIT_REACHED");
    }

    /// <summary>003 · ID 30: jurados fuera de 1–7 devuelven 422.</summary>
    [Fact]
    public async Task CreateGroup_InvalidJurors_Returns422()
    {
        var (client, _) = await Api.RegisterAsync(_factory);
        var body = new Dictionary<string, object?>
        {
            ["name"] = "Jurados raros",
            ["settings"] = new Dictionary<string, object?> { ["jurors"] = 9, ["consensus"] = 0.66, ["bond_options"] = new[] { 25, 50, 100 } }
        };

        (await client.PostAsJsonAsync("/api/v1/groups", body)).StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }
}


public sealed class UnreachableMongoFactory : PactoggWebFactory
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureAppConfiguration((_, configuration) =>
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["MongoDb:ConnectionString"] = "mongodb://127.0.0.1:1/?serverSelectionTimeoutMS=500&connectTimeoutMS=500"
            }));
    }
}

/// <summary>001 · CLARIFICACION #136: si la base está caída, /health/ready reporta fallo (F2-D54).</summary>
public sealed class HealthWithoutDatabaseTests : IClassFixture<UnreachableMongoFactory>
{
    private readonly UnreachableMongoFactory _factory;

    public HealthWithoutDatabaseTests(UnreachableMongoFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task HealthReady_WhenDatabaseIsDown_Returns503()
    {
        var response = await _factory.CreateClient().GetAsync("/health/ready");
        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        (await _factory.CreateClient().GetAsync("/health")).StatusCode.Should().Be(HttpStatusCode.OK, "la vida del proceso no depende de la base");
        (await response.Content.ReadAsStringAsync()).Should().NotContain("127.0.0.1");
    }
}
