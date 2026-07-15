using Microsoft.Extensions.Caching.Memory;
using Jellyfin.Plugin.FilmAffinity.Scraping;

namespace Jellyfin.Plugin.FilmAffinity.Api;

/// <summary>
/// Servicio de cache en memoria para resultados de FilmAffinity.
/// TTL: 6h para peliculas recientes (ultimos 2 anios), 24h para el resto.
/// Sin dependencia de SQLite ni almacenamiento persistente.
/// </summary>
public class FilmAffinityCacheService
{
    private readonly IMemoryCache _cache;
    private static readonly TimeSpan RecentTtl = TimeSpan.FromHours(6);
    private static readonly TimeSpan DefaultTtl = TimeSpan.FromHours(24);
    private const int RecentYears = 2;

    public FilmAffinityCacheService(IMemoryCache cache)
    {
        _cache = cache;
    }

    /// <summary>
    /// Intenta obtener un resultado de la cache.
    /// </summary>
    public bool TryGet(string title, int? year, out RatingResult? result)
    {
        var key = BuildKey(title, year);
        return _cache.TryGetValue(key, out result);
    }

    /// <summary>
    /// Almacena un resultado en la cache con TTL segun la antiguedad de la pelicula.
    /// Peliculas de los ultimos 2 anios: 6h. Resto: 24h.
    /// Si el anio no es parseable, se asume pelicula reciente (TTL corto de 6h)
    /// por seguridad: mejor refrescar de mas que mantener datos incorrectos.
    /// </summary>
    public void Set(string title, int? year, RatingResult result)
    {
        var key = BuildKey(title, year);
        var yearValue = result.Year is not null && int.TryParse(result.Year, out var y) ? y : 0;
        // Si no se pudo determinar el anio, usar TTL corto como fallback conservador
        var age = yearValue > 0 ? DateTime.UtcNow.Year - yearValue : 0;
        var ttl = age <= RecentYears ? RecentTtl : DefaultTtl;

        var options = new MemoryCacheEntryOptions()
            .SetAbsoluteExpiration(ttl)
            .SetSize(1);

        _cache.Set(key, result, options);
    }

    /// <summary>
    /// Construye una clave de cache unica basada en titulo normalizado y anio.
    /// </summary>
    private static string BuildKey(string title, int? year)
        => $"fa:{FilmAffinityHelpers.NormalizeSearchText(title)}:{year ?? 0}";
}
