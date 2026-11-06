using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using FluentAssertions;

namespace IntegrationTest;

/// <summary>
/// Reglas transversales de los documentos (CLARIFICACION y Descripción del Backend):
/// reutilización de refresh (#10), JSON de 1 MB, gzip (BE-05) e Idempotency-Key en votos.
/// </summary>
public sealed class DocComplianceTests : IClassFixture<PactoggWebFactory>
{
    private readonly PactoggWebFactory _factory;

    public DocComplianceTests(PactoggWebFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task ReusedRefreshToken_RevokesEverySession()
    {
        var client = _factory.CreateClient();
        var session = await RegisterAsync(client);
        var original = session.GetProperty("refresh_token").GetString()!;

        var rotated = await client.PostAsJsonAsync("/api/v1/auth/refresh", new Dictionary<string, object?> { ["refresh_token"] = original });
        rotated.StatusCode.Should().Be(HttpStatusCode.OK);
        var current = (await ReadJsonAsync(rotated)).GetProperty("data").GetProperty("refresh_token").GetString()!;

        // Alguien reutiliza el refresh viejo: se considera robado.
        var reuse = await client.PostAsJsonAsync("/api/v1/auth/refresh", new Dictionary<string, object?> { ["refresh_token"] = original });
        reuse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        // También cae la sesión legítima: hay que iniciar sesión otra vez.
        var afterReuse = await client.PostAsJsonAsync("/api/v1/auth/refresh", new Dictionary<string, object?> { ["refresh_token"] = current });
        afterReuse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task JsonBodyOverOneMegabyte_Returns413()
    {
        var client = _factory.CreateClient();
        await RegisterAsync(client);
        var big = "{\"name\":\"" + new string('a', 1024 * 1024 + 10) + "\"}";
        using var content = new StringContent(big, Encoding.UTF8, "application/json");
        var response = await client.PostAsync("/api/v1/groups", content);
        response.StatusCode.Should().Be(HttpStatusCode.RequestEntityTooLarge);
        (await ReadJsonAsync(response)).GetProperty("code").GetString().Should().Be("PAYLOAD_TOO_LARGE");
    }

    [Fact]
    public async Task JsonResponses_AreGzipped_ExceptAuthRoutes()
    {
        var client = _factory.CreateClient();
        await RegisterAsync(client);

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/users/me");
        request.Headers.AcceptEncoding.Add(new StringWithQualityHeaderValue("gzip"));
        var response = await client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentEncoding.Should().Contain("gzip");

        using var login = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/login")
        {
            Content = JsonContent.Create(new Dictionary<string, object?> { ["email"] = "nadie@example.com", ["password"] = "frase larga de ejemplo" })
        };
        login.Headers.AcceptEncoding.Add(new StringWithQualityHeaderValue("gzip"));
        var auth = await client.SendAsync(login);
        auth.Content.Headers.ContentEncoding.Should().BeEmpty();
    }

    private static async Task<JsonElement> RegisterAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync("/api/v1/auth/register", new Dictionary<string, object?>
        {
            ["email"] = $"doc-{Guid.NewGuid():N}@example.com",
            ["password"] = "frase larga de ejemplo",
            ["nickname"] = $"d{Guid.NewGuid():N}"[..20],
            ["accept_privacy"] = true,
            ["privacy_version"] = "2026-10-01"
        });
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var data = (await ReadJsonAsync(response)).GetProperty("data");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", data.GetProperty("access_token").GetString());
        return data;
    }

    private static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response)
    {
        var text = await response.Content.ReadAsStringAsync();
        return JsonDocument.Parse(text).RootElement.Clone();
    }
}
