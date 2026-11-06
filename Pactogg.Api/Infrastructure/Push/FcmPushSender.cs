using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace Pactogg.Api.Infrastructure.Push;

/// <summary>
/// Firebase Cloud Messaging HTTP v1 sin SDK: firma un JWT con la cuenta de servicio (RS256), lo cambia por un
/// token OAuth2 (se reutiliza ~55 min) y envía un mensaje por dispositivo. Los tokens UNREGISTERED se devuelven
/// como inválidos para borrarlos.
/// </summary>
public sealed class FcmPushSender : IPushSender
{
    private const string Scope = "https://www.googleapis.com/auth/firebase.messaging";

    private readonly HttpClient _http;
    private readonly ILogger<FcmPushSender> _logger;
    private readonly ServiceAccount? _account;
    private readonly string? _projectId;
    private readonly SemaphoreSlim _tokenLock = new(1, 1);
    private string? _accessToken;
    private DateTime _accessTokenExpires;

    public FcmPushSender(HttpClient http, IOptions<FirebaseOptions> options, ILogger<FcmPushSender> logger)
    {
        _http = http;
        _logger = logger;
        var value = options.Value;
        if (value.IsConfigured)
        {
            try
            {
                _account = JsonSerializer.Deserialize<ServiceAccount>(value.ServiceAccountJson!);
                _projectId = string.IsNullOrWhiteSpace(value.ProjectId) ? _account?.ProjectId : value.ProjectId;
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Firebase:ServiceAccountJson no es un JSON válido; el push queda desactivado.");
            }
        }
    }

    public bool IsConfigured => _account is { ClientEmail: not null, PrivateKey: not null } && !string.IsNullOrWhiteSpace(_projectId);

    public async Task<PushResult> SendAsync(IReadOnlyCollection<string> tokens, PushMessage message, CancellationToken cancellationToken)
    {
        if (!IsConfigured || tokens.Count == 0)
        {
            return new PushResult(0, []);
        }

        var accessToken = await GetAccessTokenAsync(cancellationToken);
        var sent = 0;
        var invalid = new List<string>();
        foreach (var token in tokens)
        {
            var payload = new
            {
                message = new
                {
                    token,
                    notification = new { title = message.Title, body = message.Body },
                    data = message.Data,
                    android = new { priority = "high", notification = new { channel_id = "pactogg_default" } }
                }
            };

            using var request = new HttpRequestMessage(HttpMethod.Post, $"https://fcm.googleapis.com/v1/projects/{_projectId}/messages:send")
            {
                Content = JsonContent.Create(payload)
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            using var response = await _http.SendAsync(request, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                sent++;
                continue;
            }

            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            if (response.StatusCode == HttpStatusCode.NotFound
                || body.Contains("UNREGISTERED", StringComparison.Ordinal)
                || (response.StatusCode == HttpStatusCode.BadRequest && body.Contains("registration token", StringComparison.OrdinalIgnoreCase)))
            {
                invalid.Add(token);
            }
            else
            {
                _logger.LogWarning("FCM respondió {Status} al enviar un aviso de tipo {Type}.", (int)response.StatusCode, message.Type);
            }
        }

        return new PushResult(sent, invalid);
    }

    private async Task<string> GetAccessTokenAsync(CancellationToken cancellationToken)
    {
        if (_accessToken is not null && DateTime.UtcNow < _accessTokenExpires)
        {
            return _accessToken;
        }

        await _tokenLock.WaitAsync(cancellationToken);
        try
        {
            if (_accessToken is not null && DateTime.UtcNow < _accessTokenExpires)
            {
                return _accessToken;
            }

            var tokenUri = _account!.TokenUri ?? "https://oauth2.googleapis.com/token";
            var assertion = CreateAssertion(_account, tokenUri);
            using var response = await _http.PostAsync(
                tokenUri,
                new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["grant_type"] = "urn:ietf:params:oauth:grant-type:jwt-bearer",
                    ["assertion"] = assertion
                }),
                cancellationToken);
            response.EnsureSuccessStatusCode();
            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
            _accessToken = document.RootElement.GetProperty("access_token").GetString();
            var expiresIn = document.RootElement.TryGetProperty("expires_in", out var exp) ? exp.GetInt32() : 3600;
            _accessTokenExpires = DateTime.UtcNow.AddSeconds(Math.Max(60, expiresIn - 300));
            return _accessToken!;
        }
        finally
        {
            _tokenLock.Release();
        }
    }

    private static string CreateAssertion(ServiceAccount account, string audience)
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var header = Base64Url(JsonSerializer.SerializeToUtf8Bytes(new { alg = "RS256", typ = "JWT" }));
        var claims = Base64Url(JsonSerializer.SerializeToUtf8Bytes(new
        {
            iss = account.ClientEmail,
            scope = Scope,
            aud = audience,
            iat = now,
            exp = now + 3600
        }));
        var unsigned = $"{header}.{claims}";
        using var rsa = RSA.Create();
        rsa.ImportFromPem(account.PrivateKey);
        var signature = rsa.SignData(Encoding.ASCII.GetBytes(unsigned), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        return $"{unsigned}.{Base64Url(signature)}";
    }

    private static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private sealed class ServiceAccount
    {
        [System.Text.Json.Serialization.JsonPropertyName("project_id")]
        public string? ProjectId { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("client_email")]
        public string? ClientEmail { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("private_key")]
        public string? PrivateKey { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("token_uri")]
        public string? TokenUri { get; set; }
    }
}
