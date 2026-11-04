using MongoDB.Bson;

namespace Pactogg.Api.Common;

/// <summary>Validación compartida de listas con cursor: limit 1–50, cursor = ObjectId, textos acotados.</summary>
public static class ListQuery
{
    public static void ValidateLimit(int limit)
    {
        if (limit is < 1 or > 50)
        {
            throw ApiException.Unprocessable(ErrorCodes.ValidationError, "limit debe estar entre 1 y 50.");
        }
    }

    public static ObjectId? ParseCursor(string? cursor)
    {
        if (string.IsNullOrWhiteSpace(cursor))
        {
            return null;
        }

        if (!ObjectId.TryParse(cursor, out var parsed))
        {
            throw ApiException.Unprocessable(ErrorCodes.ValidationError, "cursor no es válido.");
        }

        return parsed;
    }

    public static string? NormalizeText(string? value, int maxLength, string field)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        if (trimmed.Length > maxLength)
        {
            throw ApiException.Unprocessable(ErrorCodes.ValidationError, $"{field} admite máximo {maxLength} caracteres.");
        }

        return trimmed;
    }
}
