using System.Net;
using System.Text.RegularExpressions;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SecurityTest;

/// <summary>
/// Recorre TODAS las rutas que registra la API (no una lista escrita a mano):
/// cada endpoint protegido debe responder 401 sin token y con token inválido,
/// y ningún endpoint de /api queda sin una decisión explícita de autorización.
/// </summary>
public sealed class EndpointAuthorizationSweepTests
{
    private const string SampleId = "000000000000000000000000";

    // Una sola API para todo el barrido. El límite de peticiones se apaga solo aquí para que
    // el 429 no tape el 401 que se quiere comprobar; el límite tiene sus propias pruebas.
    private static readonly Lazy<WebApplicationFactory<Program>> SharedFactory = new(() =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.UseSetting("RateLimit:Enabled", "false")));

    private static WebApplicationFactory<Program> Factory => SharedFactory.Value;

    public static IEnumerable<object[]> ProtectedEndpoints() =>
        ApiEndpoints(SharedFactory.Value.Services)
            .Where(endpoint => endpoint.Protected)
            .Select(endpoint => new object[] { endpoint.Method, endpoint.Template });

    [Theory]
    [MemberData(nameof(ProtectedEndpoints))]
    public async Task EveryProtectedEndpoint_WithoutToken_Returns401(string method, string template)
    {
        using var response = await SendAsync(method, template, token: null);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized, $"{method} {template} no debe responder sin sesión");
        var body = await response.Content.ReadAsStringAsync();
        body.Should().NotContain("   at ", "no se exponen trazas internas");
    }

    [Theory]
    [MemberData(nameof(ProtectedEndpoints))]
    public async Task EveryProtectedEndpoint_WithForgedToken_Returns401(string method, string template)
    {
        // JWT con forma válida pero firma inventada: no debe aceptarse.
        const string forged = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9." +
            "eyJzdWIiOiIwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAiLCJleHAiOjQxMDI0NDQ4MDB9." +
            "c2lnbmF0dXJhLWZhbHNh";

        using var response = await SendAsync(method, template, forged);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized, $"{method} {template} no debe aceptar un token falsificado");
    }

    [Fact]
    public void EveryApiEndpoint_DeclaresAnExplicitAuthorizationDecision()
    {
        var endpoints = ApiEndpoints(Factory.Services);
        endpoints.Should().NotBeEmpty();

        var undecided = endpoints.Where(endpoint => !endpoint.Protected && !endpoint.Anonymous)
            .Select(endpoint => $"{endpoint.Method} {endpoint.Template}");

        undecided.Should().BeEmpty("cada endpoint debe llevar RequireAuthorization() o AllowAnonymous()");
    }

    [Fact]
    public void AnonymousEndpoints_AreOnlyAuthenticationAndNicknameCheck()
    {
        var endpoints = ApiEndpoints(Factory.Services);
        var anonymous = endpoints.Where(endpoint => endpoint.Anonymous).Select(endpoint => endpoint.Template).ToList();

        // Solo el registro/inicio de sesión y la consulta de apodo pueden ser anónimos.
        anonymous.Should().OnlyContain(template =>
            template.StartsWith("/api/v1/auth/", StringComparison.Ordinal) ||
            template == "/api/v1/users/check-nickname",
            "un endpoint anónimo fuera de esa lista sería una fuga de datos");
    }

    private async Task<HttpResponseMessage> SendAsync(string method, string template, string? token)
    {
        var client = Factory.CreateClient();
        using var request = new HttpRequestMessage(new HttpMethod(method), Concrete(template));
        if (token is not null)
        {
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        }
        if (method is "POST" or "PATCH" or "PUT")
        {
            request.Content = new StringContent("{}", System.Text.Encoding.UTF8, "application/json");
        }
        return await client.SendAsync(request);
    }

    private static string Concrete(string template) =>
        Regex.Replace(template, @"\{[^}]+\}", SampleId, RegexOptions.None, TimeSpan.FromSeconds(1));

    internal sealed record ApiEndpoint(string Method, string Template, bool Protected, bool Anonymous);

    internal static List<ApiEndpoint> ApiEndpoints(IServiceProvider services) =>
        services.GetServices<EndpointDataSource>()
            .SelectMany(source => source.Endpoints)
            .OfType<RouteEndpoint>()
            .Where(endpoint => (endpoint.RoutePattern.RawText ?? string.Empty).StartsWith("/api", StringComparison.Ordinal))
            .SelectMany(endpoint =>
            {
                var template = endpoint.RoutePattern.RawText!;
                var anonymous = endpoint.Metadata.GetMetadata<IAllowAnonymous>() is not null;
                var isProtected = !anonymous && endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>().Any();
                var methods = endpoint.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods ?? new[] { "GET" };
                return methods.Select(method => new ApiEndpoint(method, template, isProtected, anonymous));
            })
            .DistinctBy(endpoint => (endpoint.Method, endpoint.Template))
            .OrderBy(endpoint => endpoint.Template, StringComparer.Ordinal)
            .ThenBy(endpoint => endpoint.Method, StringComparer.Ordinal)
            .ToList();
}
