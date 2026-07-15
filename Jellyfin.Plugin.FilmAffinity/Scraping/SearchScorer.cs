namespace Jellyfin.Plugin.FilmAffinity.Scraping;

/// <summary>
/// Logica de puntuacion para ordenar los resultados de busqueda de FilmAffinity.
/// Replica el algoritmo del scraper de Cruzadera (scoreSearchCandidate).
/// Prioriza coincidencia exacta de titulo + coincidencia de anio.
/// </summary>
public static class SearchScorer
{
    /// <summary>
    /// Asigna una puntuacion a un candidato de busqueda basandose en
    /// la similitud con el titulo y anio solicitados.
    /// </summary>
    /// <param name="candidate">Resultado de busqueda a evaluar.</param>
    /// <param name="requestedTitle">Titulo original solicitado (sin normalizar).</param>
    /// <param name="requestedYear">Anio solicitado (opcional).</param>
    /// <returns>Puntuacion: mas alta = mejor coincidencia.</returns>
    public static int Score(SearchResult candidate, string requestedTitle, string? requestedYear)
    {
        var normRequested = Normalize(requestedTitle);
        var normCandidate = Normalize(candidate.Title);
        var normContext = Normalize(candidate.ContextText);
        var score = 0;

        // Coincidencia exacta de titulo: maxima prioridad
        if (normCandidate == normRequested)
        {
            score += 100;
        }
        // El titulo del candidato contiene el titulo buscado
        else if (normCandidate.Contains(normRequested))
        {
            score += 60;
        }
        // El contexto (texto alrededor del enlace) contiene el titulo buscado
        else if (normContext.Contains(normRequested))
        {
            score += 30;
        }

        // Bonus por coincidencia de anio (+/- 1 anio de tolerancia)
        if (requestedYear is not null && MatchesYear(candidate.ContextText, requestedYear))
        {
            score += 40;
        }

        // Bonus menor si la URL tiene formato /filmXXXXX (es una ficha valida)
        if (candidate.Url.Contains("/film", StringComparison.OrdinalIgnoreCase))
        {
            score += 5;
        }

        return score;
    }

    /// <summary>
    /// Normaliza un texto para comparacion: elimina acentos, convierte a minusculas,
    /// elimina caracteres no alfanumericos y colapsa espacios.
    /// Equivalente a normalizeSearchText del scraper de Cruzadera.
    /// </summary>
    public static string Normalize(string value)
    {
        return FilmAffinityHelpers.NormalizeSearchText(value);
    }

    /// <summary>
    /// Verifica si el texto de contexto contiene un anio que coincide
    /// con el anio solicitado, con tolerancia de +/- 1.
    /// </summary>
    /// <param name="contextText">Texto de contexto del candidato.</param>
    /// <param name="requestedYear">Anio solicitado como string.</param>
    /// <returns>true si algun anio en el contexto esta dentro del rango solicitado +/- 1.</returns>
    private static bool MatchesYear(string contextText, string requestedYear)
    {
        if (!int.TryParse(requestedYear, out var reqYear) || reqYear < 1900 || reqYear > 2100)
            return false;

        // Buscar todos los anios (19xx o 20xx) en el texto de contexto
        var matches = System.Text.RegularExpressions.Regex.Matches(
            contextText, @"\b(19|20)\d{2}\b");

        foreach (System.Text.RegularExpressions.Match m in matches)
        {
            if (int.TryParse(m.Value, out var year) && Math.Abs(year - reqYear) <= 1)
                return true;
        }

        return false;
    }
}
