using System.Net;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;

namespace SecurityTest;

public sealed class AuthorizationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public AuthorizationTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    [Theory]
    [InlineData("POST", "/api/v1/groups")]
    [InlineData("POST", "/api/v1/contracts")]
    [InlineData("GET", "/api/v1/tournaments")]
    [InlineData("GET", "/api/v1/users/me")]
    [InlineData("PATCH", "/api/v1/users/me")]
    [InlineData("GET", "/api/v1/groups")]
    [InlineData("POST", "/api/v1/groups/join")]
    [InlineData("GET", "/api/v1/groups/000000000000000000000000/members")]
    [InlineData("GET", "/api/v1/groups/000000000000000000000000/contracts")]
    [InlineData("GET", "/api/v1/contracts/000000000000000000000000")]
    [InlineData("POST", "/api/v1/contracts/000000000000000000000000/accept")]
    [InlineData("POST", "/api/v1/contracts/000000000000000000000000/reject")]
    public async Task ProtectedEndpoints_WithoutToken_Return401(string method, string path)
    {
        using var request = new HttpRequestMessage(new HttpMethod(method), path);
        if (method is "POST" or "PATCH")
        {
            request.Content = new StringContent("{}", System.Text.Encoding.UTF8, "application/json");
        }

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("UNAUTHORIZED");
        body.Should().NotContain("   at ");
    }

    [Fact]
    public async Task ProtectedEndpoint_WithInvalidToken_Return401()
    {
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", "not-a-jwt");

        var response = await _client.GetAsync("/api/v1/tournaments");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("UNAUTHORIZED");
        body.Should().NotContain("   at ");
    }
}
