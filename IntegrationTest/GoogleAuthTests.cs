using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;

namespace IntegrationTest;

/// <summary>AUTH-02 · ID 12: registro e inicio de sesión con Google.</summary>
public sealed class GoogleAuthTests : IClassFixture<PactoggWebFactory>
{
    private readonly PactoggWebFactory _factory;

    public GoogleAuthTests(PactoggWebFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task NewGoogleUser_NeedsNickname_ThenRegisters_ThenLogsIn()
    {
        var client = _factory.CreateClient();
        var sub = Guid.NewGuid().ToString("N");
        var email = $"g-{sub[..8]}@gmail.com";
        var token = $"google|{sub}|{email}|true";

        var missing = await client.PostAsJsonAsync("/api/v1/auth/google", new Dictionary<string, object?> { ["id_token"] = token });
        missing.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        var missingJson = await ReadJsonAsync(missing);
        missingJson.GetProperty("code").GetString().Should().Be("NICKNAME_REQUIRED");
        missingJson.GetProperty("details").GetProperty("email").GetString().Should().Be(email);

        var noPrivacy = await client.PostAsJsonAsync("/api/v1/auth/google", new Dictionary<string, object?>
        {
            ["id_token"] = token,
            ["nickname"] = $"g{sub[..10]}"
        });
        noPrivacy.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);

        var created = await client.PostAsJsonAsync("/api/v1/auth/google", new Dictionary<string, object?>
        {
            ["id_token"] = token,
            ["nickname"] = $"g{sub[..10]}",
            ["accept_privacy"] = true,
            ["privacy_version"] = "2026-10-01",
            ["device_id"] = "pixel"
        });
        created.StatusCode.Should().Be(HttpStatusCode.Created);
        var session = (await ReadJsonAsync(created)).GetProperty("data");
        session.GetProperty("access_token").GetString().Should().NotBeNullOrWhiteSpace();
        session.GetProperty("user").GetProperty("email_verified").GetBoolean().Should().BeTrue();

        var again = await client.PostAsJsonAsync("/api/v1/auth/google", new Dictionary<string, object?> { ["id_token"] = token });
        again.StatusCode.Should().Be(HttpStatusCode.OK);

        var password = await client.PostAsJsonAsync("/api/v1/auth/login", new Dictionary<string, object?>
        {
            ["email"] = email,
            ["password"] = "cualquier cosa larga"
        });
        password.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ExistingEmailAccount_IsLinked_AndInvalidTokensAreRejected()
    {
        var client = _factory.CreateClient();
        var email = $"link-{Guid.NewGuid():N}"[..20] + "@gmail.com";
        var register = await client.PostAsJsonAsync("/api/v1/auth/register", new Dictionary<string, object?>
        {
            ["email"] = email,
            ["password"] = "frase larga de ejemplo",
            ["nickname"] = $"l{Guid.NewGuid():N}"[..16],
            ["accept_privacy"] = true,
            ["privacy_version"] = "2026-10-01"
        });
        register.StatusCode.Should().Be(HttpStatusCode.Created);

        var linked = await client.PostAsJsonAsync("/api/v1/auth/google", new Dictionary<string, object?>
        {
            ["id_token"] = $"google|{Guid.NewGuid():N}|{email}|true"
        });
        linked.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ReadJsonAsync(linked)).GetProperty("data").GetProperty("user").GetProperty("email").GetString().Should().Be(email);

        var unverified = await client.PostAsJsonAsync("/api/v1/auth/google", new Dictionary<string, object?>
        {
            ["id_token"] = $"google|{Guid.NewGuid():N}|otro@gmail.com|false"
        });
        unverified.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var forged = await client.PostAsJsonAsync("/api/v1/auth/google", new Dictionary<string, object?> { ["id_token"] = "token-falso-de-prueba-123" });
        forged.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await ReadJsonAsync(forged)).GetProperty("code").GetString().Should().Be("INVALID_GOOGLE_TOKEN");
    }

    private static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response)
    {
        var stream = await response.Content.ReadAsStreamAsync();
        using var document = await JsonDocument.ParseAsync(stream);
        return document.RootElement.Clone();
    }
}
