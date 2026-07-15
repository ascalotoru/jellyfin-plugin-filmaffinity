using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Providers;
using Microsoft.Extensions.Logging;
using Jellyfin.Plugin.FilmAffinity.Api;

namespace Jellyfin.Plugin.FilmAffinity.Providers;

/// <summary>
/// Provider que aniade el rating de FilmAffinity a series de TV.
/// Misma logica que FilmAffinityMovieProvider pero para el tipo Series.
/// v2.0: Scraping nativo.
/// </summary>
public class FilmAffinitySeriesProvider : IRemoteMetadataProvider<Series, SeriesInfo>, IHasOrder
{
    private readonly FilmAffinityClient _client;
    private readonly ILogger<FilmAffinitySeriesProvider> _logger;

    public string Name => Plugin.ProviderName;
    public int Order => 1;

    public FilmAffinitySeriesProvider(
        FilmAffinityClient client,
        ILogger<FilmAffinitySeriesProvider> logger)
    {
        _client = client;
        _logger = logger;
    }

    public async Task<MetadataResult<Series>> GetMetadata(
        SeriesInfo info, CancellationToken ct)
    {
        var result = new MetadataResult<Series>();
        var config = Plugin.Instance!.Configuration;

        var faResult = await _client.GetMovie(
            info.OriginalTitle ?? info.Name,
            info.OriginalTitle is not null ? info.Name : null,
            info.Year,
            ct).ConfigureAwait(false);

        if (faResult is null)
        {
            _logger.LogDebug("FA: sin resultado para serie {Title}",
                info.OriginalTitle ?? info.Name);
            return result;
        }

        if (!FilmAffinityHelpers.HasEnoughVotes(faResult.Votes.ToString(), config.MinVotes))
            return result;

        result.HasMetadata = true;
        result.Item = new Series
        {
            CommunityRating = faResult.Rating
        };

        if (config.SetCriticRating)
            result.Item.CriticRating = faResult.Rating * 10f;

        result.Item.ProviderIds[Plugin.ProviderId] = faResult.Url ?? "rated";

        _logger.LogInformation("FA: [Serie] {Title} -> {Rating}/10",
            faResult.Title, faResult.Rating);

        return result;
    }

    public Task<IEnumerable<RemoteSearchResult>> GetSearchResults(
        SeriesInfo searchInfo, CancellationToken ct)
        => Task.FromResult(Enumerable.Empty<RemoteSearchResult>());

    public Task<HttpResponseMessage> GetImageResponse(
        string url, CancellationToken ct)
        => Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.NotFound));
}
