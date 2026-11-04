using System.Text;
using System.Text.Json;
using Pactogg.Api.Common;

namespace Pactogg.Api.Common.Middleware;

public sealed class NoSqlKeyValidationMiddleware
{
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

        context.Request.EnableBuffering();
        string body;
        using (var reader = new StreamReader(context.Request.Body, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, leaveOpen: true))
        {
            body = await reader.ReadToEndAsync(context.RequestAborted);
        }

        context.Request.Body.Position = 0;
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
