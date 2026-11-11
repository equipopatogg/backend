using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Amazon.Comprehend;
using Amazon.Comprehend.Model;
using Amazon.Rekognition;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using Amazon;
using Amazon.Runtime;
using Pactogg.Api.Infrastructure.Moderation;
using Pactogg.Api.Infrastructure.Options;
using Pactogg.Api.Modules.Maintenance;
using SkiaSharp;
using Xunit;
using EvidenceEntity = Pactogg.Api.Modules.Evidence.Domain.Evidence;

namespace IntegrationTest;

/// <summary>Spec 007: EVI-04, EVI-05, EVI-06, borrado a los 30 días y lista de términos en español (007-AC5..AC9).</summary>
public sealed class Spec007EvidenceTests : IClassFixture<PactoggWebFactory>
{
    private readonly PactoggWebFactory _factory;

    public Spec007EvidenceTests(PactoggWebFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task AC5_ReplaceBeforeVotes_SwapsPhotoAndDeletesOld_AfterAVote_Returns409()
    {
        var s = await SetupVotingAsync(jurors: 3, photo: true);
        var oldKey = _factory.Evidence.All().Single(x => x.Id.ToString() == s.EvidenceId).ObjectKey!;
        _factory.Storage.Exists(oldKey).Should().BeTrue();

        var notMine = await s.Owner.PutAsync($"/api/v1/contracts/{s.ContractId}/evidence/{s.EvidenceId}", PhotoForm());
        notMine.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await Api.Code(notMine)).Should().Be("NOT_EVIDENCE_OWNER");

        var replaced = await s.Doer.PutAsync($"/api/v1/contracts/{s.ContractId}/evidence/{s.EvidenceId}", PhotoForm(SKColors.OrangeRed));
        replaced.StatusCode.Should().Be(HttpStatusCode.OK);
        var newKey = _factory.Evidence.All().Single(x => x.Id.ToString() == s.EvidenceId).ObjectKey!;
        newKey.Should().NotBe(oldKey);
        _factory.Storage.Exists(oldKey).Should().BeFalse("la foto anterior se borra de S3");
        _factory.Storage.Exists(newKey).Should().BeTrue();

        var toLink = await s.Doer.PutAsJsonAsync($"/api/v1/contracts/{s.ContractId}/evidence/{s.EvidenceId}", Link("https://youtu.be/reemplazo1"));
        toLink.StatusCode.Should().Be(HttpStatusCode.OK);
        (await Api.Data(toLink)).GetProperty("type").GetString().Should().Be("link");
        _factory.Storage.Exists(newKey).Should().BeFalse();

        (await s.Jurors[0].PutAsJsonAsync($"/api/v1/contracts/{s.ContractId}/votes", new Dictionary<string, object?> { ["vote"] = "legit" }))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        var frozen = await s.Doer.PutAsJsonAsync($"/api/v1/contracts/{s.ContractId}/evidence/{s.EvidenceId}", Link("https://youtu.be/tarde1"));
        frozen.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await Api.Code(frozen)).Should().Be("VOTING_STARTED");
    }

    [Fact]
    public async Task AC6_DeleteOnlyEvidence_ReturnsContractToInProgress_OthersForbidden_AfterVote409()
    {
        var s = await SetupVotingAsync(jurors: 1, photo: false);

        var notMine = await s.Owner.DeleteAsync($"/api/v1/evidence/{s.EvidenceId}");
        notMine.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        (await s.Doer.DeleteAsync($"/api/v1/evidence/{s.EvidenceId}")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        var contract = _factory.Contracts.Get(ObjectId.Parse(s.ContractId));
        contract.Status.Should().Be("in_progress");
        contract.Jurors.Should().BeEmpty("los jurados quedan libres (007-D2)");
        contract.EvidenceCount.Should().Be(0);
        (await Api.Data(await s.Doer.GetAsync($"/api/v1/contracts/{s.ContractId}/evidence"))).GetArrayLength().Should().Be(0);
        (await Api.Data(await s.Jurors[0].GetAsync("/api/v1/me/votes/pending"))).GetArrayLength().Should().Be(0);
        (await s.Doer.DeleteAsync($"/api/v1/evidence/{s.EvidenceId}")).StatusCode.Should().Be(HttpStatusCode.NotFound);

        var voted = await SetupVotingAsync(jurors: 3, photo: false);
        (await voted.Jurors[0].PutAsJsonAsync($"/api/v1/contracts/{voted.ContractId}/votes", new Dictionary<string, object?> { ["vote"] = "legit" }))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        var late = await voted.Doer.DeleteAsync($"/api/v1/evidence/{voted.EvidenceId}");
        late.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await Api.Code(late)).Should().Be("VOTING_STARTED");
    }

    [Fact]
    public async Task AC7_SaveInFame_OnlyFulfilled_IsIdempotent_AndMovesPhotoOutOfThe30DayRule()
    {
        var s = await SetupVotingAsync(jurors: 1, photo: true);
        var early = await s.Owner.PostAsync($"/api/v1/evidence/{s.EvidenceId}/save-fame", null);
        early.StatusCode.Should().Be(HttpStatusCode.Conflict, "el reto todavía no se cumple");

        (await s.Jurors[0].PutAsJsonAsync($"/api/v1/contracts/{s.ContractId}/votes", new Dictionary<string, object?> { ["vote"] = "legit" }))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        _factory.Contracts.Get(ObjectId.Parse(s.ContractId)).Status.Should().Be("fulfilled");
        var oldKey = _factory.Evidence.All().Single(x => x.Id.ToString() == s.EvidenceId).ObjectKey!;

        var first = await Api.Data(await s.Owner.PostAsync($"/api/v1/evidence/{s.EvidenceId}/save-fame", null));
        first.GetProperty("saved_in_fame").GetBoolean().Should().BeTrue();
        (!first.TryGetProperty("expires_at", out var expires) || expires.ValueKind == System.Text.Json.JsonValueKind.Null)
            .Should().BeTrue("la foto de la Fama ya no tiene fecha de borrado");
        var stored = _factory.Evidence.All().Single(x => x.Id.ToString() == s.EvidenceId);
        stored.ObjectKey.Should().StartWith("fame/");
        _factory.Storage.Exists(stored.ObjectKey!).Should().BeTrue();
        _factory.Storage.Exists(oldKey).Should().BeFalse();

        (await s.Juror(0).PostAsync($"/api/v1/evidence/{s.EvidenceId}/save-fame", null)).StatusCode.Should().Be(HttpStatusCode.OK, "es idempotente");

        var (outsider, _) = await Api.RegisterAsync(_factory);
        (await outsider.PostAsync($"/api/v1/evidence/{s.EvidenceId}/save-fame", null)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task AC8_PhotosOlderThan30Days_AreDeleted_ExceptTheOnesInTheFame()
    {
        var now = DateTime.UtcNow;
        var contractId = ObjectId.GenerateNewId();
        EvidenceEntity Photo(bool fame)
        {
            var key = $"{(fame ? "fame" : "evidence")}/g/{contractId}/{ObjectId.GenerateNewId()}.jpg";
            _factory.Storage.PutAsync([1, 2, 3], "image/jpeg", key, CancellationToken.None).Wait();
            return new EvidenceEntity
            {
                Id = ObjectId.GenerateNewId(),
                ContractId = contractId,
                UserId = ObjectId.GenerateNewId(),
                Type = "photo",
                ObjectKey = key,
                SavedInFame = fame,
                Moderation = new() { Status = "approved" },
                CreatedAt = now.AddDays(-31),
                ExpiresAt = fame ? null : now.AddDays(-1)
            };
        }

        var old = Photo(fame: false);
        var kept = Photo(fame: true);
        await _factory.Evidence.CreateAsync(old, CancellationToken.None);
        await _factory.Evidence.CreateAsync(kept, CancellationToken.None);

        using var scope = _factory.Services.CreateScope();
        var expired = await scope.ServiceProvider.GetRequiredService<MaintenanceService>().ExpirePhotosAsync(now, CancellationToken.None);

        expired.Should().BeGreaterThanOrEqualTo(1);
        old.Moderation.Status.Should().Be("expired");
        _factory.Storage.Exists(old.ObjectKey!).Should().BeFalse();
        kept.Moderation.Status.Should().Be("approved");
        _factory.Storage.Exists(kept.ObjectKey!).Should().BeTrue();

        // La regla del bucket (deploy/terraform/storage.tf) solo aplica al prefijo evidence/.
        var storageTf = Path.Combine(FindRepoRoot(), "deploy", "terraform", "storage.tf");
        if (File.Exists(storageTf))
        {
            var tf = await File.ReadAllTextAsync(storageTf);
            tf.Should().Contain("aws_s3_bucket_lifecycle_configuration").And.Contain("prefix = \"evidence/\"").And.Contain("days = 30");
        }
    }

    [Fact]
    public async Task AC9_SpanishTerm_IsRejectedBeforeComprehend()
    {
        var shipped = _factory.Services.GetRequiredService<IOptions<ModerationOptions>>().Value.BlockedTerms;
        shipped.Should().Contain("pendejo", "la lista en español viene en appsettings.json (007-D3)");

        var counter = new CountingComprehend();
        var options = Options.Create(new ModerationOptions { BlockedTerms = shipped.ToList() });
        var service = new AwsModerationService(new AmazonRekognitionClient(new AnonymousAWSCredentials(), RegionEndpoint.USEast1), counter, options);

        (await service.ModerateTextAsync("Ganar sin ser un PENDEJO", CancellationToken.None)).Approved.Should().BeFalse();
        (await service.ModerateTextAsync("no seas p3nd3j0", CancellationToken.None)).Approved.Should().BeFalse("se normalizan sustituciones comunes");
        (await service.ModerateTextAsync("Chinga tu madre", CancellationToken.None)).Approved.Should().BeFalse();
        counter.Calls.Should().Be(0, "con un término de la lista no se llama a Comprehend");

        (await service.ModerateTextAsync("Gana con la computadora del cibercafé", CancellationToken.None)).Approved.Should().BeTrue("solo cuenta la palabra completa");
        counter.Calls.Should().Be(1);

        var local = new LocalModerationService(options);
        (await local.ModerateTextAsync("eres un culero", CancellationToken.None)).Approved.Should().BeFalse();
        (await local.ModerateTextAsync("Ganar tres partidas seguidas", CancellationToken.None)).Approved.Should().BeTrue();
    }

    private sealed record Setup(HttpClient Owner, HttpClient Doer, List<HttpClient> Jurors, string GroupId, string ContractId, string EvidenceId)
    {
        public HttpClient Juror(int i) => Jurors[i];
    }

    /// <summary>Grupo con <paramref name="jurors"/> jurados, reto aceptado por el retado y una evidencia (foto o enlace) en voting.</summary>
    private async Task<Setup> SetupVotingAsync(int jurors, bool photo)
    {
        var (owner, _) = await Api.RegisterAsync(_factory);
        var (doer, doerUser) = await Api.RegisterAsync(_factory);
        var body = Api.Group("Evidencias");
        body["settings"] = new Dictionary<string, object?> { ["jurors"] = jurors, ["consensus"] = 0.66, ["bond_options"] = new[] { 25, 50, 100 } };
        var created = await Api.Data(await owner.PostAsJsonAsync("/api/v1/groups", body));
        var groupId = created.GetProperty("id").GetString()!;
        var code = created.GetProperty("invite_code").GetString()!;
        await Api.JoinAsync(doer, code);
        var jurorClients = new List<HttpClient>();
        for (var i = 0; i < jurors; i++)
        {
            var (juror, _) = await Api.RegisterAsync(_factory);
            await Api.JoinAsync(juror, code);
            jurorClients.Add(juror);
        }

        var contractId = await Api.CreateContractAsync(owner, groupId, doerUser.Id);
        (await doer.PostAsync($"/api/v1/contracts/{contractId}/accept", null)).StatusCode.Should().Be(HttpStatusCode.OK);
        var evidence = photo
            ? await doer.PostAsync($"/api/v1/contracts/{contractId}/evidence", PhotoForm())
            : await doer.PostAsJsonAsync($"/api/v1/contracts/{contractId}/evidence", Link("https://www.youtube.com/watch?v=evid007"));
        evidence.StatusCode.Should().Be(HttpStatusCode.Created);
        var evidenceId = (await Api.Data(evidence)).GetProperty("id").GetString()!;
        _factory.Contracts.Get(ObjectId.Parse(contractId)).Status.Should().Be("voting");
        return new Setup(owner, doer, jurorClients, groupId, contractId, evidenceId);
    }

    private static Dictionary<string, object?> Link(string url) => new() { ["type"] = "link", ["url"] = url };

    private static MultipartFormDataContent PhotoForm(SKColor? color = null)
    {
        using var bitmap = new SKBitmap(4, 4);
        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(color ?? SKColors.CornflowerBlue);
        }

        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(data.ToArray());
        file.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        form.Add(file, "file", "evidence.png");
        return form;
    }

    /// <summary>Comprehend falso: cuenta las llamadas a DetectToxicContentAsync y responde "no tóxico" sin salir a la red.</summary>
    private sealed class CountingComprehend : AmazonComprehendClient
    {
        public CountingComprehend()
            : base(new AnonymousAWSCredentials(), RegionEndpoint.USEast1)
        {
        }

        public int Calls { get; private set; }

        public override Task<DetectToxicContentResponse> DetectToxicContentAsync(DetectToxicContentRequest request, CancellationToken cancellationToken = default)
        {
            Calls++;
            return Task.FromResult(new DetectToxicContentResponse { ResultList = [new ToxicLabels { Toxicity = 0.01f, Labels = [] }] });
        }
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "deploy")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? AppContext.BaseDirectory;
    }
}
