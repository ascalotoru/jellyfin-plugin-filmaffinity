using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Providers;
using Microsoft.Extensions.Logging;
using Jellyfin.Plugin.FilmAffinity.Api;

namespace Jellyfin.Plugin.FilmAffinity.Providers;

/// <summary>
/// Provider que aniade el rating de FilmAffinity a sagas y colecciones (BoxSet).
/// FilmAffinity tiene paginas dedicadas para sagas (ej: "El Senor de los Anillos (Saga)").
/// v2.0: Scraping nativo.
/// </summary>
public class FilmAffinityBoxSetProvider : IRemoteMetadataProvider<BoxSet, BoxSetInfo>, IHasOrder
{
    private readonly FilmAffinityClient _client;
    private readonly ILogger<FilmAffinityBoxSetProvider> _logger;

    public string Name => Plugin.ProviderName;
    public int Order => 1;

    public FilmAffinityBoxSetProvider(
        FilmAffinityClient client,
        ILogger<FilmAffinityBoxSetProvider> logger)
    {
        _client = client;
        _logger = logger;
    }

    public async Task<MetadataResult<BoxSet>> GetMetadata(
        BoxSetInfo info, CancellationToken ct)
    {
        var result = new MetadataResult<BoxSet>();
        var config = Plugin.Instance!.Configuration;

        // Las sagas no suelen tener OriginalTitle separado; se usa Name directamente
        var faResult = await _client.GetMovie(
            info.Name, null, info.Year, ct).ConfigureAwait(false);

        if (faResult is null)
            return result;

        if (!FilmAffinityHelpers.HasEnoughVotes(faResult.Votes.ToString(), config.MinVotes))
            return result;

        result.HasMetadata = true;
        result.Item = new BoxSet
        {
            CommunityRating = faResult.Rating
        };

        if (config.SetCriticRating)
            result.Item.CriticRating = faResult.Rating * 10f;

        result.Item.ProviderIds[Plugin.ProviderId] = faResult.Url ?? "rated";

        _logger.LogInformation("FA: [Saga] {Title} -> {Rating}/10",
            faResult.Title, faResult.Rating);

        return result;
    }

    public Task<IEnumerable<RemoteSearchResult>> GetSearchResults(
        BoxSetInfo searchInfo, CancellationToken ct)
        => Task.FromResult(Enumerable.Empty<RemoteSearchResult>());

    public Task<HttpResponseMessage> GetImageResponse(
        string url, CancellationToken ct)
        => Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.NotFound));
}
