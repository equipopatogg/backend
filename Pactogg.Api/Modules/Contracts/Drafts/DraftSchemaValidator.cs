using System.Text.Json;

namespace Pactogg.Api.Modules.Contracts.Drafts;

/// <summary>Salida del modelo ya validada: { rival, game, condition, reward }.</summary>
public sealed record DraftOutput(string? Rival, string? Game, string Condition, string? Reward);

/// <summary>
/// 005-R5: la respuesta del modelo es no confiable. Debe ser un objeto JSON con exactamente las claves
/// rival (string|null), game (string|null), condition (string) y reward (string|null); cualquier otra clave o tipo la invalida.
/// </summary>
public static class DraftSchemaValidator
{
    private static readonly string[] Keys = ["rival", "game", "condition", "reward"];

    public static DraftOutput? TryParse(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw) || raw.Length > 4000)
        {
            return null;
        }

        var text = StripFence(raw.Trim());
        try
        {
            using var document = JsonDocument.Parse(text);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            var names = root.EnumerateObject().Select(x => x.Name).ToList();
            if (names.Count != Keys.Length || names.Distinct(StringComparer.Ordinal).Count() != Keys.Length || !Keys.All(names.Contains))
            {
                return null;
            }

            if (!TryString(root.GetProperty("rival"), nullable: true, out var rival)
                || !TryString(root.GetProperty("game"), nullable: true, out var game)
                || !TryString(root.GetProperty("condition"), nullable: false, out var condition)
                || !TryString(root.GetProperty("reward"), nullable: true, out var reward))
            {
                return null;
            }

            return new DraftOutput(Clean(rival), Clean(game), condition!.Trim(), Clean(reward));
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static bool TryString(JsonElement element, bool nullable, out string? value)
    {
        value = null;
        if (element.ValueKind == JsonValueKind.Null)
        {
            return nullable;
        }

        if (element.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        value = element.GetString();
        return true;
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    /// <summary>Algunos modelos envuelven el JSON en ```json … ```; se quita la envoltura, no se corrige el contenido.</summary>
    private static string StripFence(string text)
    {
        if (!text.StartsWith("```", StringComparison.Ordinal))
        {
            return text;
        }

        var firstLine = text.IndexOf('\n');
        var last = text.LastIndexOf("```", StringComparison.Ordinal);
        return firstLine > 0 && last > firstLine ? text[(firstLine + 1)..last].Trim() : text;
    }
}
