namespace Jellyfin.Plugin.FilmAffinity.Scraping;

/// <summary>
/// Resultado de una busqueda en la pagina de busqueda de FilmAffinity.
/// Representa un enlace a una ficha de pelicula/serie encontrada.
/// </summary>
public record SearchResult(
    string Title,
    string Url,
    string ContextText,
    int Score
);

/// <summary>
/// Resultado final del scraping de una ficha de FilmAffinity.
/// Contiene la valoracion, votos, titulo y anio extraidos del HTML.
/// </summary>
public record RatingResult(
    string Title,
    string? Year,
    float Rating,
    int Votes,
    string Url
);
