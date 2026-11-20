using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;

namespace SecurityTest;

/// <summary>/health y /health/ready son públicos (los usa Caddy) y no deben exponer detalles internos.</summary>
public sealed class HealthExposureTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public HealthExposureTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    [Theory]
    [InlineData("/health")]
    [InlineData("/health/ready")]
    public async Task HealthEndpoints_DoNotLeakInternals(string path)
    {
        var response = await _client.GetAsync(path);

        ((int)response.StatusCode).Should().BeOneOf(200, 503);
        var body = await response.Content.ReadAsStringAsync();
        foreach (var secret in new[] { "mongodb", "redis://", "password", "Password", "   at ", "Exception", "ConnectionString" })
        {
            body.Should().NotContain(secret, $"{path} no debe exponer {secret}");
        }

        response.Headers.GetValues("X-Content-Type-Options").Should().ContainSingle("nosniff");
    }
}
