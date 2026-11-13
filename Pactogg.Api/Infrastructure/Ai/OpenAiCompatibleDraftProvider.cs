using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Pactogg.Api.Infrastructure.Options;

namespace Pactogg.Api.Infrastructure.Ai;

/// <summary>
/// Proveedor genérico para APIs compatibles con OpenAI (<c>POST {Endpoint}/chat/completions</c>): Groq, OpenRouter,
/// Mistral, Ollama o Gemini en su modo OpenAI. Temperatura 0 y salida JSON. No escribe en logs la frase ni la respuesta (005-R15).
/// </summary>
public sealed class OpenAiCompatibleDraftProvider : IContractDraftProvider
{
    private readonly HttpClient _http;
    private readonly AiOptions _options;

    public OpenAiCompatibleDraftProvider(HttpClient http, IOptions<AiOptions> options)
    {
        _http = http;
        _options = options.Value;
    }

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(_options.Endpoint) && !string.IsNullOrWhiteSpace(_options.Model);

    public async Task<string> CompleteAsync(string systemPrompt, string userData, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, _options.Endpoint.TrimEnd('/') + "/chat/completions")
        {
            Content = JsonContent.Create(new
            {
                model = _options.Model,
                temperature = 0,
                response_format = new { type = "json_object" },
                messages = new object[]
                {
                    new { role = "system", content = systemPrompt },
                    new { role = "user", content = userData }
                }
            })
        };
        if (!string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);
        }

        using var response = await _http.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(cancellationToken), cancellationToken: cancellationToken);
        return document.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString() ?? string.Empty;
    }
}
