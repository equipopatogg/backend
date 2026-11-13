using System.Text.Json;
using MongoDB.Driver;
using Pactogg.Api.Common;

namespace Pactogg.Api.Common.Middleware;

public sealed class ErrorHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ErrorHandlingMiddleware> _logger;

    public ErrorHandlingMiddleware(RequestDelegate next, ILogger<ErrorHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
        }
        catch (ApiException ex)
        {
            await ErrorResponseWriter.WriteAsync(context, ex.StatusCode, ex.Code, ex.Message, ex.Details, retryAfterSeconds: ex.RetryAfterSeconds);
        }
        catch (BadHttpRequestException ex)
        {
            if (ex.StatusCode == StatusCodes.Status413PayloadTooLarge && !context.Request.HasFormContentType)
            {
                await ErrorResponseWriter.WriteAsync(context, StatusCodes.Status413PayloadTooLarge, ErrorCodes.PayloadTooLarge, "El cuerpo de la solicitud es demasiado grande.");
                return;
            }

            var status = ex.StatusCode == StatusCodes.Status413PayloadTooLarge
                ? StatusCodes.Status422UnprocessableEntity
                : StatusCodes.Status400BadRequest;
            var code = ex.StatusCode == StatusCodes.Status413PayloadTooLarge
                ? ErrorCodes.ImageTooLarge
                : ErrorCodes.InvalidInput;
            await ErrorResponseWriter.WriteAsync(context, status, code, "Solicitud inválida.");
        }
        catch (JsonException)
        {
            await ErrorResponseWriter.WriteAsync(context, StatusCodes.Status400BadRequest, ErrorCodes.InvalidInput, "Cuerpo JSON inválido.");
        }
        catch (MongoException ex)
        {
            _logger.LogError(ex, "Error de MongoDB en la solicitud.");
            await ErrorResponseWriter.WriteAsync(
                context,
                StatusCodes.Status503ServiceUnavailable,
                ErrorCodes.DatabaseUnavailable,
                "La base de datos no está disponible.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error no controlado en la solicitud.");
            await ErrorResponseWriter.WriteAsync(
                context,
                StatusCodes.Status500InternalServerError,
                ErrorCodes.InternalError,
                "Ocurrió un error interno.");
        }
    }
}
