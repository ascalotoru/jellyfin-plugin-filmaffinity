using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Providers;
using Microsoft.Extensions.Logging;
using Jellyfin.Plugin.FilmAffinity.Api;

namespace Jellyfin.Plugin.FilmAffinity.Providers;

/// <summary>
/// Provider que aniade el rating de FilmAffinity a peliculas.
/// Se ejecuta despues de los providers principales (TMDB, TVDB, etc.)
/// gracias a IHasOrder.Order = 1. No reemplaza metadatos existentes,
/// solo complementa con el rating de FA.
/// v2.0: Scraping nativo.
/// </summary>
public class FilmAffinityMovieProvider : IRemoteMetadataProvider<Movie, MovieInfo>, IHasOrder
{
    private readonly FilmAffinityClient _client;
    private readonly ILogger<FilmAffinityMovieProvider> _logger;

    public string Name => Plugin.ProviderName;

    /// <summary>
    /// Orden 1 = se ejecuta despues de los providers principales
    /// (que tienen orden negativo o 0). Asi no interfiere con TMDB/TVDB.
    /// </summary>
    public int Order => 1;

    public FilmAffinityMovieProvider(
        FilmAffinityClient client,
        ILogger<FilmAffinityMovieProvider> logger)
    {
        _client = client;
        _logger = logger;
    }

    /// <summary>
    /// Obtiene el rating de FilmAffinity para una pelicula.
    /// Jellyfin llama a este metodo durante el escaneo de bibliotecas.
    /// </summary>
    public async Task<MetadataResult<Movie>> GetMetadata(
        MovieInfo info, CancellationToken ct)
    {
        var result = new MetadataResult<Movie>();
        var config = Plugin.Instance!.Configuration;

        // El cliente implementa la estrategia: titulo original -> localizado -> sin anio
        var faResult = await _client.GetMovie(
            info.OriginalTitle ?? info.Name,
            info.OriginalTitle is not null ? info.Name : null,
            info.Year,
            ct).ConfigureAwait(false);

        if (faResult is null)
        {
            _logger.LogDebug("FA: sin resultado para {Title}", info.OriginalTitle ?? info.Name);
            return result;
        }

        // Validar que el rating tiene suficientes votos para ser fiable
        if (!FilmAffinityHelpers.HasEnoughVotes(faResult.Votes.ToString(), config.MinVotes))
        {
            _logger.LogDebug("FA: {Title} -> {Rating}/10 pero solo {Votes} votos (< {Min})",
                faResult.Title, faResult.Rating, faResult.Votes, config.MinVotes);
            return result;
        }

        // Escribir el rating en el campo CommunityRating de Jellyfin
        result.HasMetadata = true;
        result.Item = new Movie
        {
            CommunityRating = faResult.Rating
        };

        // Opcional: establecer tambien CriticRating (FA rating * 10)
        if (config.SetCriticRating)
            result.Item.CriticRating = faResult.Rating * 10f;

        // Marcar el item como procesado para que el SyncTask no lo reprocese
        result.Item.ProviderIds[Plugin.ProviderId] = faResult.Url ?? "rated";

        _logger.LogInformation("FA: {Title} ({Year}) -> {Rating}/10 [{Votes} votos]",
            faResult.Title, faResult.Year, faResult.Rating, faResult.Votes);

        return result;
    }

    /// <summary>
    /// No implementamos busqueda completa. Solo enriquecemos ratings.
    /// </summary>
    public Task<IEnumerable<RemoteSearchResult>> GetSearchResults(
        MovieInfo searchInfo, CancellationToken ct)
        => Task.FromResult(Enumerable.Empty<RemoteSearchResult>());

    /// <summary>
    /// No gestionamos imagenes.
    /// </summary>
    public Task<HttpResponseMessage> GetImageResponse(
        string url, CancellationToken ct)
        => Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.NotFound));
}
