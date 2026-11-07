using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using MongoDB.Bson;
using Xunit;

namespace IntegrationTest;

/// <summary>Spec 006: límite de 5 dispositivos y limpieza al cerrar sesión en todos lados (006-AC3, 006-AC4).</summary>
public sealed class Spec006PushTests : IClassFixture<PactoggWebFactory>
{
    private readonly PactoggWebFactory _factory;

    public Spec006PushTests(PactoggWebFactory factory)
    {
        _factory = factory;
    }

    private static Dictionary<string, object?> Device(int i) => new()
    {
        ["fcm_token"] = $"token-de-prueba-{i:D2}-{Guid.NewGuid():N}",
        ["platform"] = "android"
    };

    [Fact]
    public async Task SixthDevice_ReplacesTheLeastRecentlyUsed()
    {
        var (client, user) = await Api.RegisterAsync(_factory);
        var first = Device(1);
        (await client.PutAsJsonAsync("/api/v1/devices/me", first)).StatusCode.Should().Be(HttpStatusCode.OK);
        for (var i = 2; i <= 6; i++)
        {
            await Task.Delay(5);
            (await client.PutAsJsonAsync("/api/v1/devices/me", Device(i))).StatusCode.Should().Be(HttpStatusCode.OK);
        }

        var devices = await _factory.Devices.ListByUsersAsync([ObjectId.Parse(user.Id)], CancellationToken.None);
        devices.Should().HaveCount(5);
        devices.Select(d => d.FcmToken).Should().NotContain((string)first["fcm_token"]!);
    }

    [Fact]
    public async Task LogoutEverywhere_RemovesAllDevices()
    {
        var (client, user) = await Api.RegisterAsync(_factory);
        (await client.PutAsJsonAsync("/api/v1/devices/me", Device(1))).StatusCode.Should().Be(HttpStatusCode.OK);
        (await client.PutAsJsonAsync("/api/v1/devices/me", Device(2))).StatusCode.Should().Be(HttpStatusCode.OK);

        (await client.PostAsJsonAsync("/api/v1/auth/logout", new Dictionary<string, object?> { ["all"] = true }))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await _factory.Devices.ListByUsersAsync([ObjectId.Parse(user.Id)], CancellationToken.None)).Should().BeEmpty();
    }
}
