namespace Pactogg.Api.Common;

public sealed class ApiException : Exception
{
    public ApiException(int statusCode, string code, string message, object? details = null)
        : base(message)
    {
        StatusCode = statusCode;
        Code = code;
        Details = details;
    }

    public int StatusCode { get; }
    public string Code { get; }
    public object? Details { get; }

    /// <summary>Segundos para la cabecera Retry-After en respuestas 429 lanzadas desde un servicio.</summary>
    public int? RetryAfterSeconds { get; init; }

    public static ApiException BadRequest(string code, string message, object? details = null) =>
        new(StatusCodes.Status400BadRequest, code, message, details);

    public static ApiException Unauthorized(string code = ErrorCodes.Unauthorized, string message = "Autenticación requerida.") =>
        new(StatusCodes.Status401Unauthorized, code, message);

    public static ApiException PayloadTooLarge(string message) =>
        new(StatusCodes.Status413PayloadTooLarge, ErrorCodes.PayloadTooLarge, message);

    public static ApiException PaymentRequired(string code, string message) =>
        new(StatusCodes.Status402PaymentRequired, code, message);

    public static ApiException Forbidden(string code, string message) =>
        new(StatusCodes.Status403Forbidden, code, message);

    public static ApiException NotFound(string message = "El recurso no existe.") =>
        new(StatusCodes.Status404NotFound, ErrorCodes.NotFound, message);

    public static ApiException Conflict(string code, string message) =>
        new(StatusCodes.Status409Conflict, code, message);

    public static ApiException Unprocessable(string code, string message, object? details = null) =>
        new(StatusCodes.Status422UnprocessableEntity, code, message, details);

    public static ApiException TooManyRequests(string message = "Demasiadas solicitudes. Intenta más tarde.") =>
        new(StatusCodes.Status429TooManyRequests, ErrorCodes.RateLimited, message);

    public static ApiException ServiceUnavailable(string code, string message) =>
        new(StatusCodes.Status503ServiceUnavailable, code, message);
}
