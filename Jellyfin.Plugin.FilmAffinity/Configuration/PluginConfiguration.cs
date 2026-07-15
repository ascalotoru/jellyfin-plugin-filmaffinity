using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.FilmAffinity.Configuration;

/// <summary>
/// Configuracion persistente del plugin FilmAffinity v2.0.
/// Se serializa a XML en /config/plugins/FilmAffinity.xml
/// Scraping nativo: sin dependencia de API externa.
/// </summary>
public class PluginConfiguration : BasePluginConfiguration
{
    /// <summary>URL base de FilmAffinity (incluye subdirectorio de idioma)</summary>
    public string FilmAffinityBaseUrl { get; set; } = "https://www.filmaffinity.com/es/";

    /// <summary>Timeout en segundos para cada request HTTP a FilmAffinity</summary>
    public int RequestTimeoutSeconds { get; set; } = 30;

    /// <summary>User-Agent enviado en las peticiones HTTP</summary>
    public string UserAgent { get; set; } = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0.0.0 Safari/537.36";

    /// <summary>Minimo de votos en FA para considerar la valoracion fiable</summary>
    public int MinVotes { get; set; } = 50;

    /// <summary>Si es true, tambien escribe CriticRating (FA rating x 10)</summary>
    public bool SetCriticRating { get; set; } = false;

    /// <summary>Delay entre requests HTTP a FilmAffinity (ms)</summary>
    public int ThrottleMs { get; set; } = 2000;

    /// <summary>Intervalo de sincronizacion programada en horas</summary>
    public int SyncIntervalHours { get; set; } = 24;
}
