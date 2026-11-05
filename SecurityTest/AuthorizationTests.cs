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
    [InlineData("GET", "/api/v1/contracts/000000000000000000000000/evidence")]
    [InlineData("GET", "/api/v1/me/votes/pending")]
    [InlineData("PUT", "/api/v1/contracts/000000000000000000000000/votes")]
    [InlineData("GET", "/api/v1/contracts/000000000000000000000000/votes/summary")]
    [InlineData("POST", "/api/v1/contracts/000000000000000000000000/votes/resolve")]
    [InlineData("GET", "/api/v1/contracts/000000000000000000000000/result")]
    [InlineData("GET", "/api/v1/groups/000000000000000000000000/points/me")]
    [InlineData("GET", "/api/v1/groups/000000000000000000000000/points/ranking")]
    [InlineData("GET", "/api/v1/groups/000000000000000000000000/commitments")]
    [InlineData("POST", "/api/v1/commitments/000000000000000000000000/complete")]
    [InlineData("POST", "/api/v1/commitments/000000000000000000000000/request-complete")]
    [InlineData("GET", "/api/v1/notifications")]
    [InlineData("PATCH", "/api/v1/notifications/000000000000000000000000/read")]
    [InlineData("POST", "/api/v1/notifications/read-all")]
    [InlineData("GET", "/api/v1/groups/000000000000000000000000/fame")]
    public async Task ProtectedEndpoints_WithoutToken_Return401(string method, string path)
    {
        using var request = new HttpRequestMessage(new HttpMethod(method), path);
        if (method is "POST" or "PATCH" or "PUT")
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
