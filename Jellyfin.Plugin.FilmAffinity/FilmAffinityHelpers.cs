namespace Jellyfin.Plugin.FilmAffinity;

/// <summary>
/// Metodos auxiliares compartidos entre providers y el SyncTask.
/// Centraliza el parseo de votos y normalizacion de texto para evitar duplicacion.
/// </summary>
public static class FilmAffinityHelpers
{
    /// <summary>
    /// Convierte el string de votos de FilmAffinity ("12.345", "1,234", "173.755")
    /// a entero, eliminando separadores de miles.
    /// </summary>
    /// <param name="votes">String de votos extraido del HTML de FA.</param>
    /// <returns>Numero de votos como entero, o 0 si no se pudo parsear.</returns>
    public static int ParseVotes(string? votes)
    {
        if (string.IsNullOrWhiteSpace(votes))
            return 0;

        // Eliminar puntos (estilo espaniol: "173.755") y comas (estilo ingles: "173,755")
        return int.TryParse(
            votes.Replace(".", "").Replace(",", ""),
            out var v) ? v : 0;
    }

    /// <summary>
    /// Verifica si un rating de FilmAffinity tiene suficientes votos
    /// para ser considerado fiable, segun la configuracion del plugin.
    /// </summary>
    /// <param name="votes">String de votos extraido del HTML de FA (ej: "173.755").</param>
    /// <param name="minVotes">Umbral minimo de votos desde la configuracion.</param>
    /// <returns>true si los votos alcanzan o superan el minimo.</returns>
    public static bool HasEnoughVotes(string? votes, int minVotes)
    {
        return ParseVotes(votes) >= minVotes;
    }

    /// <summary>
    /// Normaliza un texto para busqueda y comparacion en FilmAffinity.
    /// Elimina acentos (NFD), convierte a minusculas, quita guiones y
    /// colapsa todos los caracteres no alfanumericos en espacios.
    /// Equivalente a la funcion normalizeSearchText del scraper de Cruzadera.
    /// Ejemplo: "  Amelie  " -> "amelie"
    /// </summary>
    /// <param name="value">Texto a normalizar (titulo de pelicula/serie).</param>
    /// <returns>Texto normalizado, sin acentos ni caracteres especiales, en minusculas.</returns>
    public static string NormalizeSearchText(string value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        // NFD descompone caracteres Unicode (ej: "e" -> "e" + combining acute)
        // Luego eliminamos los combining marks (rango U+0300-U+036f)
        var normalized = value
            .Normalize(System.Text.NormalizationForm.FormD)
            .Replace("-", " ")          // guiones -> espacio
            .ToLowerInvariant();

        // Eliminar combining diacritical marks manteniendo solo caracteres base
        var result = new System.Text.StringBuilder(normalized.Length);
        foreach (var c in normalized)
        {
            var cat = System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c);
            if (cat != System.Globalization.UnicodeCategory.NonSpacingMark)
            {
                // Colapsar caracteres no alfanumericos (salvo digitos y letras) a espacio
                result.Append(char.IsLetterOrDigit(c) ? c : ' ');
            }
        }

        // Colapsar multiples espacios consecutivos y recortar extremos
        return System.Text.RegularExpressions.Regex.Replace(
            result.ToString(), @"\s+", " ").Trim();
    }
}
