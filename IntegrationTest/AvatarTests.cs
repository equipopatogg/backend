using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using SkiaSharp;
using Xunit;

namespace IntegrationTest;

/// <summary>USR-07: foto de perfil moderada, visible para el grupo y quitada al elegir un color.</summary>
public sealed class AvatarTests : IClassFixture<PactoggWebFactory>
{
    private readonly PactoggWebFactory _factory;

    public AvatarTests(PactoggWebFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task UploadedPhoto_IsShownInProfileAndMembers_AndColorRemovesIt()
    {
        var (owner, ownerUser) = await Api.RegisterAsync(_factory);
        var (friend, _) = await Api.RegisterAsync(_factory);
        var (groupId, code) = await Api.CreateGroupAsync(owner);
        await Api.JoinAsync(friend, code);

        var uploaded = await owner.PostAsync("/api/v1/users/me/avatar", Photo());
        uploaded.StatusCode.Should().Be(HttpStatusCode.OK);
        var profile = await Api.Data(uploaded);
        profile.GetProperty("avatar_id").GetString().Should().Be("photo");
        var url = profile.GetProperty("avatar_url").GetString()!;
        url.Should().StartWith("https://cdn.example/avatars/" + ownerUser.Id + "/");
        var key = url["https://cdn.example/".Length..];
        _factory.Storage.Exists(key).Should().BeTrue();

        var members = await Api.Data(await friend.GetAsync($"/api/v1/groups/{groupId}/members"));
        members.EnumerateArray().Single(x => x.GetProperty("user_id").GetString() == ownerUser.Id)
            .GetProperty("avatar_url").GetString().Should().Be(url);

        var color = await owner.PatchAsJsonAsync("/api/v1/users/me", new Dictionary<string, object?> { ["avatar_id"] = "cyan" });
        color.StatusCode.Should().Be(HttpStatusCode.OK);
        var after = await Api.Data(color);
        after.GetProperty("avatar_id").GetString().Should().Be("cyan");
        if (after.TryGetProperty("avatar_url", out var gone))
        {
            gone.ValueKind.Should().Be(JsonValueKind.Null);
        }

        _factory.Storage.Exists(key).Should().BeFalse("la foto anterior se borra de S3");
    }

    [Fact]
    public async Task RejectedOrWrongFile_IsNotStored()
    {
        var (client, _) = await Api.RegisterAsync(_factory);
        var before = _factory.Storage.PutCount;

        var text = new MultipartFormDataContent();
        var file = new ByteArrayContent("hola"u8.ToArray());
        file.Headers.ContentType = new MediaTypeHeaderValue("text/plain");
        text.Add(file, "file", "a.txt");
        (await client.PostAsync("/api/v1/users/me/avatar", text)).StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);

        _factory.Moderation.RejectImages = true;
        try
        {
            var rejected = await client.PostAsync("/api/v1/users/me/avatar", Photo());
            rejected.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
            (await Api.Code(rejected)).Should().Be("CONTENT_REJECTED");
        }
        finally
        {
            _factory.Moderation.RejectImages = false;
        }

        _factory.Storage.PutCount.Should().Be(before);
        (await client.PatchAsJsonAsync("/api/v1/users/me", new Dictionary<string, object?> { ["avatar_id"] = "photo" }))
            .StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    private static MultipartFormDataContent Photo()
    {
        using var bitmap = new SKBitmap(8, 8);
        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(SKColors.MediumPurple);
        }

        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(data.ToArray());
        file.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        form.Add(file, "file", "avatar.png");
        return form;
    }
}
