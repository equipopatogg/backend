using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Pactogg.Api.Infrastructure.Moderation;

/// <summary>
/// 007-D3: lista local de términos en español (Moderation:BlockedTerms) que se revisa antes de Comprehend,
/// porque la detección de toxicidad de Comprehend solo evalúa inglés.
/// Compara palabras completas sin mayúsculas ni acentos y con sustituciones comunes (0→o, 1→i, 3→e, 4→a, @→a, $→s),
/// así "computadora" no coincide con un término contenido dentro de ella.
/// </summary>
public sealed class BlockedTermsMatcher
{
    private readonly IReadOnlyList<(string Term, Regex Pattern)> _patterns;

    public BlockedTermsMatcher(IEnumerable<string> terms)
    {
        _patterns = terms
            .Select(Normalize)
            .Where(x => x.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .Select(term => (term, new Regex(
                $@"(?<![a-zñ]){string.Join(@"[^a-zñ]+", term.Split(' ').Select(Regex.Escape))}(e?s)?(?![a-zñ])",
                RegexOptions.CultureInvariant | RegexOptions.Compiled,
                TimeSpan.FromMilliseconds(200))))
            .ToList();
    }

    public bool IsEmpty => _patterns.Count == 0;

    /// <summary>Términos de la lista que aparecen en el texto (vacío si ninguno).</summary>
    public IReadOnlyList<string> Find(string? text)
    {
        if (string.IsNullOrWhiteSpace(text) || _patterns.Count == 0)
        {
            return [];
        }

        var normalized = Normalize(text);
        var matches = new List<string>();
        foreach (var (term, pattern) in _patterns)
        {
            try
            {
                if (pattern.IsMatch(normalized))
                {
                    matches.Add(term);
                }
            }
            catch (RegexMatchTimeoutException)
            {
                // Falla cerrada: si no se pudo revisar, se trata como coincidencia.
                matches.Add(term);
            }
        }

        return matches;
    }

    public static string Normalize(string text)
    {
        var decomposed = text.ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        for (var i = 0; i < decomposed.Length; i++)
        {
            var c = decomposed[i];
            var category = CharUnicodeInfo.GetUnicodeCategory(c);
            if (category == UnicodeCategory.NonSpacingMark)
            {
                // La ñ se descompone en n + tilde: se conserva como ñ.
                if (c == '̃' && builder.Length > 0 && builder[^1] == 'n')
                {
                    builder[^1] = 'ñ';
                }

                continue;
            }

            builder.Append(c switch
            {
                '0' => 'o',
                '1' => 'i',
                '3' => 'e',
                '4' => 'a',
                '@' => 'a',
                '$' => 's',
                _ => c
            });
        }

        return Regex.Replace(builder.ToString(), @"\s+", " ").Trim();
    }
}
