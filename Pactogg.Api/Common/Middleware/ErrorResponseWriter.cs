using Pactogg.Api.Common;

namespace Pactogg.Api.Common.Middleware;

public static class ErrorResponseWriter
{
    public static async Task WriteAsync(
        HttpContext context,
        int statusCode,
        string code,
        string message,
        object? details = null,
        CancellationToken cancellationToken = default,
        int? retryAfterSeconds = null)
    {
        if (context.Response.HasStarted)
        {
            return;
        }

        context.Response.Clear();
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/json; charset=utf-8";
        if (retryAfterSeconds is > 0)
        {
            context.Response.Headers["Retry-After"] = retryAfterSeconds.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        var requestId = context.Items["request_id"]?.ToString() ?? context.TraceIdentifier;
        await context.Response.WriteAsJsonAsync(new
        {
            code,
            message,
            request_id = requestId,
            details
        }, ApiResults.JsonOptions, cancellationToken: cancellationToken);
    }
}
