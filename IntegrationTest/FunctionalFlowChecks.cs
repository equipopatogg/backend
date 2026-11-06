using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using SkiaSharp;

namespace IntegrationTest;

public static class FunctionalFlowChecks
{
    public static async Task AssertAuthAndModerationBeforeStorageAsync(PactoggWebFactory factory)
    {
        var client = factory.CreateClient();
        var email = $"flow-{Guid.NewGuid():N}@example.com";
        const string password = "frase larga de ejemplo";

        var register = await client.PostAsJsonAsync("/api/v1/auth/register", new Dictionary<string, object?>
        {
            ["email"] = email,
            ["password"] = password,
            ["nickname"] = $"flow{Guid.NewGuid():N}"[..20],
            ["accept_privacy"] = true,
            ["privacy_version"] = "2026-10-01",
            ["device_id"] = "functional-flow"
        });
        register.StatusCode.Should().Be(HttpStatusCode.Created);

        var login = await client.PostAsJsonAsync("/api/v1/auth/login", new Dictionary<string, object?>
        {
            ["email"] = email,
            ["password"] = password,
            ["device_id"] = "functional-flow"
        });
        login.StatusCode.Should().Be(HttpStatusCode.OK);
        var accessToken = (await ReadJsonAsync(login)).GetProperty("data").GetProperty("access_token").GetString();
        accessToken.Should().NotBeNullOrWhiteSpace();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var group = await client.PostAsJsonAsync("/api/v1/groups", new Dictionary<string, object?>
        {
            ["name"] = $"Grupo {Guid.NewGuid():N}"[..20]
        });
        group.StatusCode.Should().Be(HttpStatusCode.Created);
        var groupId = (await ReadJsonAsync(group)).GetProperty("data").GetProperty("id").GetString();
        groupId.Should().NotBeNullOrWhiteSpace();
        factory.PadMembers(groupId!);

        var contract = await client.PostAsJsonAsync("/api/v1/contracts", new Dictionary<string, object?>
        {
            ["group_id"] = groupId,
            ["game"] = "Valorant",
            ["condition"] = "Ganar tres partidas seguidas",
            ["reward"] = "Elegir el próximo juego",
            ["deadline"] = DateTimeOffset.UtcNow.AddDays(7).ToString("O"),
            ["bond_required"] = 50,
            ["target_type"] = "group",
            ["source"] = "manual"
        });
        contract.StatusCode.Should().Be(HttpStatusCode.Created);
        var contractId = (await ReadJsonAsync(contract)).GetProperty("data").GetProperty("id").GetString();
        contractId.Should().NotBeNullOrWhiteSpace();

        var putsBefore = factory.Storage.PutCount;
        factory.Moderation.RejectImages = true;
        try
        {
            using var form = new MultipartFormDataContent();
            form.Add(new StringContent("photo"), "type");
            var file = new ByteArrayContent(CreatePng());
            file.Headers.ContentType = new MediaTypeHeaderValue("image/png");
            form.Add(file, "file", "evidence.png");

            var rejected = await client.PostAsync($"/api/v1/contracts/{contractId}/evidence", form);
            rejected.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
            (await ReadJsonAsync(rejected)).GetProperty("code").GetString().Should().Be("CONTENT_REJECTED");
            factory.Storage.PutCount.Should().Be(putsBefore);
        }
        finally
        {
            factory.Moderation.RejectImages = false;
        }

        var logout = await client.PostAsJsonAsync("/api/v1/auth/logout", new Dictionary<string, object?>
        {
            ["all"] = false
        });
        logout.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var afterLogout = await client.GetAsync("/api/v1/tournaments");
        afterLogout.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    private static byte[] CreatePng()
    {
        using var bitmap = new SKBitmap(2, 2);
        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(SKColors.CornflowerBlue);
        }

        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    private static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response)
    {
        var stream = await response.Content.ReadAsStreamAsync();
        using var document = await JsonDocument.ParseAsync(stream);
        return document.RootElement.Clone();
    }
}
