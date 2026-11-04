using System.Net;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;

namespace ContractTest;

public sealed class OpenApiContractTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public OpenApiContractTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }


    [Fact]
    public void OpenApiFile_DocumentsEvaluationEndpoints()
    {
        var text = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "openapi.yaml"));

        foreach (var path in new[]
                 {
                     "/api/v1/auth/register",
                     "/api/v1/auth/login",
                     "/api/v1/auth/logout",
                     "/api/v1/groups:",
                     "/api/v1/contracts:",
                     "/api/v1/contracts/{id}/evidence",
                     "/api/v1/tournaments:",
                     "/api/v1/users/me:",
                     "/api/v1/users/check-nickname:",
                     "/api/v1/groups/join:",
                     "/api/v1/groups/{id}:",
                     "/api/v1/groups/{id}/members:",
                     "/api/v1/contracts/{id}:",
                     "/api/v1/contracts/{id}/accept:",
                     "/api/v1/contracts/{id}/reject:"
                 })
        {
            text.Should().Contain(path);
        }

        text.Should().Contain("minLength: 3, maxLength: 80");
    }

    [Fact]
    public async Task Swagger_DocumentsAllEvaluationEndpoints()
    {
        var response = await _client.GetAsync("/swagger/v1/swagger.json");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var paths = document.RootElement.GetProperty("paths");

        paths.TryGetProperty("/api/v1/auth/register", out _).Should().BeTrue();
        paths.TryGetProperty("/api/v1/auth/login", out _).Should().BeTrue();
        paths.TryGetProperty("/api/v1/auth/logout", out _).Should().BeTrue();
        paths.TryGetProperty("/api/v1/groups", out _).Should().BeTrue();
        paths.TryGetProperty("/api/v1/contracts", out _).Should().BeTrue();
        paths.TryGetProperty("/api/v1/contracts/{id}/evidence", out _).Should().BeTrue();
        paths.TryGetProperty("/api/v1/tournaments", out _).Should().BeTrue();
        paths.TryGetProperty("/api/v1/users/me", out _).Should().BeTrue();
        paths.TryGetProperty("/api/v1/groups/join", out _).Should().BeTrue();
        paths.TryGetProperty("/api/v1/contracts/{id}/accept", out _).Should().BeTrue();
    }
}
