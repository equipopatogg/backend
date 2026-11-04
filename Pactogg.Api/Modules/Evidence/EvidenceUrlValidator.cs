using Pactogg.Api.Common;

namespace Pactogg.Api.Modules.Evidence;

public static class EvidenceUrlValidator
{
    public static Uri Validate(string url, IEnumerable<string> allowedHosts)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)
            || !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
            || !uri.IsDefaultPort
            || !string.IsNullOrEmpty(uri.UserInfo)
            || string.IsNullOrWhiteSpace(uri.Host))
        {
            throw ApiException.Unprocessable(ErrorCodes.InvalidEvidenceUrl, "El enlace debe ser HTTPS válido.");
        }

        var host = uri.Host.TrimEnd('.').ToLowerInvariant();
        var normalized = host.StartsWith("www.", StringComparison.Ordinal) ? host[4..] :
            host.StartsWith("m.", StringComparison.Ordinal) ? host[2..] : host;
        if (!allowedHosts.Contains(normalized, StringComparer.OrdinalIgnoreCase))
        {
            throw ApiException.Unprocessable(ErrorCodes.InvalidEvidenceUrl, "El dominio del enlace no está permitido.");
        }

        return uri;
    }
}
