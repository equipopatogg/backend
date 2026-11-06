using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using MongoDB.Bson;

namespace IntegrationTest;

/// <summary>Con verificación de correo obligatoria (CLARIFICACION #5), como quedará en producción con SES.</summary>
public sealed class VerifiedEmailFactory : PactoggWebFactory
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureAppConfiguration((_, configuration) =>
            configuration.AddInMemoryCollection(new Dictionary<string, string?> { ["Auth:RequireVerifiedEmail"] = "true" }));
    }
}

/// <summary>AUTH-05/06/07, cambio de contraseña, USR-05/07 y verificación obligatoria para grupos.</summary>
public sealed class AccountFlowTests : IClassFixture<VerifiedEmailFactory>
{
    private const string Password = "frase larga de ejemplo";
    private readonly VerifiedEmailFactory _factory;

    public AccountFlowTests(VerifiedEmailFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Register_SendsVerification_AndGroupsRequireVerifiedEmail()
    {
        var (client, user) = await Api.RegisterAsync(_factory);
        var token = _factory.Email.LastTokenFor(user.Email, "Verifica");
        token.Should().NotBeNullOrWhiteSpace();

        var blocked = await client.PostAsJsonAsync("/api/v1/groups", Api.Group("Sin verificar"));
        blocked.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await Api.Code(blocked)).Should().Be("EMAIL_NOT_VERIFIED");

        (await client.PostAsync("/api/v1/auth/verify-email/resend", null)).StatusCode.Should().Be(HttpStatusCode.Accepted);
        var latest = _factory.Email.LastTokenFor(user.Email, "Verifica")!;

        var anonymous = _factory.CreateClient();
        var verified = await anonymous.PostAsJsonAsync("/api/v1/auth/verify-email", new Dictionary<string, object?> { ["token"] = latest });
        verified.StatusCode.Should().Be(HttpStatusCode.OK);
        (await Api.Data(verified)).GetProperty("email_verified").GetBoolean().Should().BeTrue();

        var reused = await anonymous.PostAsJsonAsync("/api/v1/auth/verify-email", new Dictionary<string, object?> { ["token"] = latest });
        reused.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await Api.Code(reused)).Should().Be("INVALID_OR_EXPIRED_TOKEN");

        (await Api.Data(await client.GetAsync("/api/v1/users/me"))).GetProperty("email_verified").GetBoolean().Should().BeTrue();
        (await client.PostAsJsonAsync("/api/v1/groups", Api.Group("Verificado"))).StatusCode.Should().Be(HttpStatusCode.Created);

        var again = await client.PostAsync("/api/v1/auth/verify-email/resend", null);
        again.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await Api.Code(again)).Should().Be("EMAIL_ALREADY_VERIFIED");
    }

    [Fact]
    public async Task ForgotAndReset_AreGeneric_SingleUse_AndRevokeSessions()
    {
        var (_, user) = await Api.RegisterAsync(_factory);
        var anonymous = _factory.CreateClient();
        var before = _factory.Email.Sent.Count;

        // Correo inexistente: misma respuesta y ningún correo.
        (await anonymous.PostAsJsonAsync("/api/v1/auth/forgot-password", new Dictionary<string, object?> { ["email"] = $"nadie-{Guid.NewGuid():N}@example.com" }))
            .StatusCode.Should().Be(HttpStatusCode.Accepted);
        _factory.Email.Sent.Count.Should().Be(before);

        (await anonymous.PostAsJsonAsync("/api/v1/auth/forgot-password", new Dictionary<string, object?> { ["email"] = user.Email.ToUpperInvariant() }))
            .StatusCode.Should().Be(HttpStatusCode.Accepted);
        var token = _factory.Email.LastTokenFor(user.Email, "Restablece")!;
        token.Should().NotBeNullOrWhiteSpace();

        var weak = await anonymous.PostAsJsonAsync("/api/v1/auth/reset-password", new Dictionary<string, object?> { ["token"] = token, ["new_password"] = "corta" });
        weak.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);

        const string newPassword = "otra frase larga y nueva";
        (await anonymous.PostAsJsonAsync("/api/v1/auth/reset-password", new Dictionary<string, object?> { ["token"] = token, ["new_password"] = newPassword }))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        (await anonymous.PostAsJsonAsync("/api/v1/auth/reset-password", new Dictionary<string, object?> { ["token"] = token, ["new_password"] = newPassword }))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);

        // La sesión anterior ya no se puede renovar.
        (await anonymous.PostAsJsonAsync("/api/v1/auth/refresh", new Dictionary<string, object?> { ["refresh_token"] = user.RefreshToken }))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await Api.LoginAsync(anonymous, user.Email, Password)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var login = await Api.LoginAsync(anonymous, user.Email, newPassword);
        login.StatusCode.Should().Be(HttpStatusCode.OK);
        (await Api.Data(login)).GetProperty("user").GetProperty("email_verified").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task ChangePassword_RequiresCurrent_AndReturnsFreshSession()
    {
        var (client, user) = await Api.RegisterAsync(_factory);

        var wrong = await client.PostAsJsonAsync("/api/v1/users/me/password", new Dictionary<string, object?> { ["current_password"] = "no es esta para nada", ["new_password"] = "una frase completamente nueva" });
        wrong.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await Api.Code(wrong)).Should().Be("INVALID_CREDENTIALS");

        var changed = await client.PostAsJsonAsync("/api/v1/users/me/password", new Dictionary<string, object?> { ["current_password"] = Password, ["new_password"] = "una frase completamente nueva" });
        changed.StatusCode.Should().Be(HttpStatusCode.OK);
        var session = await Api.Data(changed);
        session.GetProperty("access_token").GetString().Should().NotBeNullOrWhiteSpace();

        var anonymous = _factory.CreateClient();
        (await anonymous.PostAsJsonAsync("/api/v1/auth/refresh", new Dictionary<string, object?> { ["refresh_token"] = user.RefreshToken }))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await anonymous.PostAsJsonAsync("/api/v1/auth/refresh", new Dictionary<string, object?> { ["refresh_token"] = session.GetProperty("refresh_token").GetString() }))
            .StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Consent_OnlyAcceptsCurrentVersion()
    {
        var (client, _) = await Api.RegisterAsync(_factory);
        (await client.PostAsJsonAsync("/api/v1/users/me/consent", new Dictionary<string, object?> { ["version"] = "2020-01-01" }))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var ok = await client.PostAsJsonAsync("/api/v1/users/me/consent", new Dictionary<string, object?> { ["version"] = "2026-10-01" });
        ok.StatusCode.Should().Be(HttpStatusCode.OK);
        (await Api.Data(ok)).GetProperty("version").GetString().Should().Be("2026-10-01");
    }
}

/// <summary>USR-05, CON-04/05, AUD-01, NOT-04 y push (DEV-01..03).</summary>
public sealed class ProfileContractAuditPushTests : IClassFixture<PactoggWebFactory>
{
    private readonly PactoggWebFactory _factory;

    public ProfileContractAuditPushTests(PactoggWebFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task PublicProfile_OnlyForSharedGroups_AndNeverShowsEmail()
    {
        var (owner, ownerUser) = await Api.RegisterAsync(_factory);
        var (friend, friendUser) = await Api.RegisterAsync(_factory);
        var (stranger, _) = await Api.RegisterAsync(_factory);
        var (_, code) = await Api.CreateGroupAsync(owner);
        await Api.JoinAsync(friend, code);

        var visible = await owner.GetAsync($"/api/v1/users/{friendUser.Id}");
        visible.StatusCode.Should().Be(HttpStatusCode.OK);
        var profile = await Api.Data(visible);
        profile.GetProperty("nickname").GetString().Should().Be(friendUser.Nickname);
        profile.TryGetProperty("email", out _).Should().BeFalse();
        profile.GetProperty("stats").GetProperty("word_index").GetInt32().Should().Be(50);

        var hidden = await stranger.GetAsync($"/api/v1/users/{ownerUser.Id}");
        hidden.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await Api.Code(hidden)).Should().Be("NO_SHARED_GROUP");
        (await owner.GetAsync($"/api/v1/users/{ObjectId.GenerateNewId()}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Challenger_EditsAndCancelsOpenContract_AdminSeesAudit()
    {
        var (owner, _) = await Api.RegisterAsync(_factory);
        var (member, memberUser) = await Api.RegisterAsync(_factory);
        var (groupId, code) = await Api.CreateGroupAsync(owner);
        await Api.JoinAsync(member, code);
        _factory.PadMembers(groupId, 1);

        var contractId = await Api.CreateContractAsync(owner, groupId, null);
        var notMine = await member.PatchAsJsonAsync($"/api/v1/contracts/{contractId}", new Dictionary<string, object?> { ["version"] = 1, ["reward"] = "Unas pizzas" });
        notMine.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await Api.Code(notMine)).Should().Be("NOT_CONTRACT_OWNER");

        var edited = await Api.Data(await owner.PatchAsJsonAsync($"/api/v1/contracts/{contractId}", new Dictionary<string, object?> { ["version"] = 1, ["reward"] = "Unas pizzas", ["bond_required"] = 50 }));
        edited.GetProperty("reward").GetString().Should().Be("Unas pizzas");
        edited.GetProperty("bond_required").GetInt32().Should().Be(50);
        edited.GetProperty("version").GetInt32().Should().Be(2);

        var stale = await owner.PatchAsJsonAsync($"/api/v1/contracts/{contractId}", new Dictionary<string, object?> { ["version"] = 1, ["game"] = "LoL" });
        stale.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await owner.PatchAsJsonAsync($"/api/v1/contracts/{contractId}", new Dictionary<string, object?> { ["game"] = "LoL" }))
            .StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);

        (await owner.DeleteAsync($"/api/v1/contracts/{contractId}?version=2")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await Api.Data(await owner.GetAsync($"/api/v1/contracts/{contractId}"))).GetProperty("status").GetString().Should().Be("cancelled");

        // Aceptado ya no se puede editar ni cancelar.
        var accepted = await Api.CreateContractAsync(owner, groupId, null);
        (await member.PostAsync($"/api/v1/contracts/{accepted}/accept", null)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await owner.PatchAsJsonAsync($"/api/v1/contracts/{accepted}", new Dictionary<string, object?> { ["version"] = 2, ["reward"] = "Otra cosa" }))
            .StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await owner.DeleteAsync($"/api/v1/contracts/{accepted}")).StatusCode.Should().Be(HttpStatusCode.Conflict);

        // AUD-01: solo admin, con el historial de lo que pasó.
        (await member.GetAsync($"/api/v1/groups/{groupId}/audit")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var audit = await owner.GetAsync($"/api/v1/groups/{groupId}/audit?entity=contract&entity_id={contractId}");
        audit.StatusCode.Should().Be(HttpStatusCode.OK);
        (await Api.Data(audit)).EnumerateArray().Select(x => x.GetProperty("action").GetString())
            .Should().ContainInOrder("contract.cancelled", "contract.updated", "contract.created");
        _ = memberUser;
    }

    [Fact]
    public async Task Push_RespectsPreferences_AndDevicesCanBeRemoved()
    {
        var (owner, _) = await Api.RegisterAsync(_factory);
        var (target, targetUser) = await Api.RegisterAsync(_factory);
        var (groupId, code) = await Api.CreateGroupAsync(owner);
        await Api.JoinAsync(target, code);
        _factory.PadMembers(groupId, 1);
        await _factory.DrainPushAsync();

        var token = $"fcm-{Guid.NewGuid():N}-token";
        var device = await target.PutAsJsonAsync("/api/v1/devices/me", new Dictionary<string, object?> { ["fcm_token"] = token, ["platform"] = "android", ["app_version"] = "1.0.0" });
        device.StatusCode.Should().Be(HttpStatusCode.OK);

        await Api.CreateContractAsync(owner, groupId, targetUser.Id);
        await _factory.DrainPushAsync();
        _factory.Push.Sent.Should().Contain(x => x.Token == token && x.Message.Type == "contract_received");

        // Sin avisos de retos: no llega push, pero sí queda en el centro de notificaciones.
        var prefs = await Api.Data(await target.PatchAsJsonAsync("/api/v1/devices/me/preferences", new Dictionary<string, object?> { ["retos"] = false }));
        prefs.GetProperty("retos").GetBoolean().Should().BeFalse();
        prefs.GetProperty("votos").GetBoolean().Should().BeTrue();
        var sentBefore = _factory.Push.Sent.Count(x => x.Token == token);
        await Api.CreateContractAsync(owner, groupId, targetUser.Id);
        await _factory.DrainPushAsync();
        _factory.Push.Sent.Count(x => x.Token == token).Should().Be(sentBefore);
        (await Api.Data(await target.GetAsync("/api/v1/devices/me/preferences"))).GetProperty("retos").GetBoolean().Should().BeFalse();

        // NOT-04: borrar un aviso propio; el de otro no existe para mí.
        var notifications = await Api.Data(await target.GetAsync("/api/v1/notifications"));
        var first = notifications.EnumerateArray().First().GetProperty("id").GetString();
        (await owner.DeleteAsync($"/api/v1/notifications/{first}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await target.DeleteAsync($"/api/v1/notifications/{first}")).StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Al cerrar sesión la app quita el dispositivo.
        await target.PatchAsJsonAsync("/api/v1/devices/me/preferences", new Dictionary<string, object?> { ["retos"] = true });
        (await target.DeleteAsync($"/api/v1/devices/me?fcm_token={token}")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        var afterRemoval = _factory.Push.Sent.Count(x => x.Token == token);
        await Api.CreateContractAsync(owner, groupId, targetUser.Id);
        await _factory.DrainPushAsync();
        _factory.Push.Sent.Count(x => x.Token == token).Should().Be(afterRemoval);
    }

    [Fact]
    public async Task UnregisteredFcmToken_IsDeleted()
    {
        var (owner, _) = await Api.RegisterAsync(_factory);
        var (target, targetUser) = await Api.RegisterAsync(_factory);
        var (groupId, code) = await Api.CreateGroupAsync(owner);
        await Api.JoinAsync(target, code);
        _factory.PadMembers(groupId, 1);

        var token = $"fcm-{Guid.NewGuid():N}-dead";
        _factory.Push.Unregistered.Add(token);
        await target.PutAsJsonAsync("/api/v1/devices/me", new Dictionary<string, object?> { ["fcm_token"] = token, ["platform"] = "android" });
        await Api.CreateContractAsync(owner, groupId, targetUser.Id);
        await _factory.DrainPushAsync();

        (await _factory.Devices.ListByUsersAsync([ObjectId.Parse(targetUser.Id)], CancellationToken.None)).Should().BeEmpty();
    }
}

public sealed record ApiUser(string Id, string Email, string Nickname, string RefreshToken);

internal static class Api
{
    public static async Task<(HttpClient Client, ApiUser User)> RegisterAsync(PactoggWebFactory factory)
    {
        var client = factory.CreateClient();
        var nickname = $"a{Guid.NewGuid():N}"[..20];
        var email = $"acc-{Guid.NewGuid():N}@example.com";
        var response = await client.PostAsJsonAsync("/api/v1/auth/register", new Dictionary<string, object?>
        {
            ["email"] = email,
            ["password"] = "frase larga de ejemplo",
            ["nickname"] = nickname,
            ["accept_privacy"] = true,
            ["privacy_version"] = "2026-10-01"
        });
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var data = await Data(response);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", data.GetProperty("access_token").GetString());
        return (client, new ApiUser(data.GetProperty("user").GetProperty("id").GetString()!, email, nickname, data.GetProperty("refresh_token").GetString()!));
    }

    public static Task<HttpResponseMessage> LoginAsync(HttpClient client, string email, string password) =>
        client.PostAsJsonAsync("/api/v1/auth/login", new Dictionary<string, object?> { ["email"] = email, ["password"] = password });

    public static Dictionary<string, object?> Group(string name) => new()
    {
        ["name"] = name,
        ["settings"] = new Dictionary<string, object?> { ["jurors"] = 1, ["consensus"] = 0.66, ["bond_options"] = new[] { 25, 50, 100 } }
    };

    public static async Task<(string Id, string Code)> CreateGroupAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync("/api/v1/groups", Group("Cuenta"));
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var data = await Data(response);
        return (data.GetProperty("id").GetString()!, data.GetProperty("invite_code").GetString()!);
    }

    public static async Task JoinAsync(HttpClient client, string code) =>
        (await client.PostAsJsonAsync("/api/v1/groups/join", new Dictionary<string, object?> { ["code"] = code }))
            .StatusCode.Should().Be(HttpStatusCode.OK);

    public static async Task<string> CreateContractAsync(HttpClient client, string groupId, string? targetId)
    {
        var response = await client.PostAsJsonAsync($"/api/v1/groups/{groupId}/contracts", new Dictionary<string, object?>
        {
            ["game"] = "Valorant",
            ["condition"] = "Ganar tres partidas seguidas",
            ["reward"] = "Unos tacos al pastor",
            ["deadline"] = DateTimeOffset.UtcNow.AddDays(7).ToString("O"),
            ["bond_required"] = 25,
            ["target_type"] = targetId is null ? "group" : "member",
            ["target_id"] = targetId,
            ["source"] = "manual"
        });
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await Data(response)).GetProperty("id").GetString()!;
    }

    public static async Task<JsonElement> Data(HttpResponseMessage response)
    {
        var json = await ReadJsonAsync(response);
        json.TryGetProperty("data", out var data).Should().BeTrue($"se esperaba data y llegó {(int)response.StatusCode}: {json}");
        return data;
    }

    public static async Task<string?> Code(HttpResponseMessage response) =>
        (await ReadJsonAsync(response)).GetProperty("code").GetString();

    private static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response)
    {
        var text = await response.Content.ReadAsStringAsync();
        return JsonDocument.Parse(text).RootElement.Clone();
    }
}
