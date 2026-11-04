using System.Text.Json;
using System.Text.Json.Serialization;

namespace Pactogg.Api.Common;

public sealed record ApiEnvelope<T>(T Data, object? Meta = null);

public static class ApiResults
{
    public static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DictionaryKeyPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static IResult Ok<T>(T data, object? meta = null) =>
        Results.Json(new ApiEnvelope<T>(data, meta), JsonOptions, statusCode: StatusCodes.Status200OK);

    public static IResult Created<T>(T data) =>
        Results.Json(new ApiEnvelope<T>(data), JsonOptions, statusCode: StatusCodes.Status201Created);
}
