using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http.Features;
using Pactogg.Api.Common;

namespace Pactogg.Api.Common.Middleware;

/// <summary>
/// Rechaza claves peligrosas para NoSQL ($, puntos) y limita el JSON a 1 MB (DESC_BACK 9).
/// Las subidas multipart conservan su propio límite de 5 MB + margen.
/// </summary>
public sealed class NoSqlKeyValidationMiddleware
{
    public const int MaxJsonBytes = 1024 * 1024;

    private readonly RequestDelegate _next;

    public NoSqlKeyValidationMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (!context.Request.HasJsonContentType())
        {
            await _next(context);
            return;
        }

        if (context.Request.ContentLength > MaxJsonBytes)
        {
            throw ApiException.PayloadTooLarge("El cuerpo JSON supera 1 MB.");
        }

        var sizeFeature = context.Features.Get<IHttpMaxRequestBodySizeFeature>();
        if (sizeFeature is { IsReadOnly: false })
        {
            sizeFeature.MaxRequestBodySize = MaxJsonBytes;
        }

        context.Request.EnableBuffering();
        string body;
        using (var reader = new StreamReader(context.Request.Body, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, leaveOpen: true))
        {
            body = await reader.ReadToEndAsync(context.RequestAborted);
        }

        context.Request.Body.Position = 0;
        if (Encoding.UTF8.GetByteCount(body) > MaxJsonBytes)
        {
            throw ApiException.PayloadTooLarge("El cuerpo JSON supera 1 MB.");
        }

        if (string.IsNullOrWhiteSpace(body))
        {
            await _next(context);
            return;
        }

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(body);
        }
        catch (JsonException)
        {
            throw ApiException.BadRequest(ErrorCodes.InvalidInput, "Cuerpo JSON inválido.");
        }

        using (document)
        {
            if (ContainsDangerousKey(document.RootElement))
            {
                throw ApiException.BadRequest(ErrorCodes.InvalidInput, "El cuerpo contiene claves no permitidas.");
            }
        }

        await _next(context);
    }

    private static bool ContainsDangerousKey(JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject())
                {
                    if (property.Name.Contains('$', StringComparison.Ordinal) || property.Name.Contains('.', StringComparison.Ordinal))
                    {
                        return true;
                    }

                    if (ContainsDangerousKey(property.Value))
                    {
                        return true;
                    }
                }

                return false;
            case JsonValueKind.Array:
                return element.EnumerateArray().Any(ContainsDangerousKey);
            default:
                return false;
        }
    }
}
