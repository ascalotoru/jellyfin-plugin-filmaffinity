using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Tasks;
using Jellyfin.Data.Enums;
using Microsoft.Extensions.Logging;
using Jellyfin.Plugin.FilmAffinity.Api;

namespace Jellyfin.Plugin.FilmAffinity.ScheduledTasks;

/// <summary>
/// Tarea programada que recorre la biblioteca en busca de items
/// sin el ProviderId de FilmAffinity y les asigna el rating via scraping directo.
/// Se ejecuta diariamente a las 3 AM por defecto.
/// v2.0: Scraping nativo, sin API externa. Cada item se busca individualmente en FA.
/// </summary>
public class FilmAffinitySyncTask : IScheduledTask
{
    private readonly ILibraryManager _libraryManager;
    private readonly FilmAffinityClient _client;
    private readonly ILogger<FilmAffinitySyncTask> _logger;

    public string Name => "Sincronizar valoraciones de FilmAffinity";
    public string Key => "FilmAffinityBatchSync";
    public string Description =>
        "Recorre la biblioteca y actualiza las valoraciones de FilmAffinity " +
        "para items que aun no tengan el ProviderId de FilmAffinity. " +
        "Scraping nativo v2.0, sin dependencias externas.";
    public string Category => "FilmAffinity";

    public FilmAffinitySyncTask(
        ILibraryManager libraryManager,
        FilmAffinityClient client,
        ILogger<FilmAffinitySyncTask> logger)
    {
        _libraryManager = libraryManager;
        _client = client;
        _logger = logger;
    }

    /// <summary>
    /// Ejecuta la sincronizacion masiva de ratings mediante scraping directo.
    /// Itera sobre items sin ProviderId FilmAffinity, buscando cada uno
    /// individualmente en FilmAffinity.com.
    /// </summary>
    public async Task ExecuteAsync(IProgress<double> progress, CancellationToken ct)
    {
        var config = Plugin.Instance!.Configuration;

        // 1. Obtener items sin ProviderId de FilmAffinity (no procesados aun)
        var allItems = _libraryManager.GetItemList(new InternalItemsQuery
        {
            IncludeItemTypes = new[] { BaseItemKind.Movie, BaseItemKind.Series, BaseItemKind.BoxSet },
            IsVirtualItem = false,
            Recursive = true
        })
        .Where(i => !i.ProviderIds.ContainsKey(Plugin.ProviderId))
        .ToList();

        _logger.LogInformation(
            "FA Sync: {Count} items sin ProviderId FilmAffinity encontrados",
            allItems.Count);

        if (allItems.Count == 0)
        {
            progress.Report(100);
            return;
        }

        var total = allItems.Count;
        var updated = 0;
        var skipped = 0;

        // 2. Procesar cada item individualmente (scraping secuencial)
        for (var i = 0; i < allItems.Count; i++)
        {
            ct.ThrowIfCancellationRequested();

            var item = allItems[i];
            var title = item.OriginalTitle ?? item.Name ?? string.Empty;
            var year = item.ProductionYear;

            try
            {
                // Buscar y obtener rating de FilmAffinity
                var searchUrl = await _client.SearchAsync(title, year, ct)
                    .ConfigureAwait(false);

                if (searchUrl is null)
                {
                    _logger.LogDebug("FA Sync: sin resultado para {Title} ({Year})",
                        title, year);
                    skipped++;
                }
                else
                {
                    var ratingResult = await _client.GetRatingAsync(
                        searchUrl, title, year, ct).ConfigureAwait(false);

                    if (ratingResult is not null
                        && FilmAffinityHelpers.HasEnoughVotes(
                            ratingResult.Votes.ToString(), config.MinVotes))
                    {
                        item.CommunityRating = ratingResult.Rating;
                        item.ProviderIds[Plugin.ProviderId] = ratingResult.Url ?? "rated";

                        if (config.SetCriticRating)
                            item.CriticRating = ratingResult.Rating * 10f;

                        // Persistir los cambios en la base de datos de Jellyfin
                        await _libraryManager.UpdateItemAsync(
                            item, item.GetParent(),
                            ItemUpdateType.MetadataEdit, ct)
                            .ConfigureAwait(false);

                        updated++;
                        _logger.LogDebug("FA Sync: {Title} ({Year}) -> {Rating}/10 [{Votes} votos]",
                            ratingResult.Title, ratingResult.Year, ratingResult.Rating, ratingResult.Votes);
                    }
                    else
                    {
                        // Marcar como buscado para evitar reintentos perpetuos.
                        // Si FA devolvio la ficha pero sin rating suficiente,
                        // no vale la pena reintentarlo cada dia.
                        item.ProviderIds[Plugin.ProviderId] = "searched";
                        await _libraryManager.UpdateItemAsync(
                            item, item.GetParent(),
                            ItemUpdateType.MetadataEdit, ct)
                            .ConfigureAwait(false);

                        skipped++;
                        _logger.LogDebug("FA Sync: {Title} ({Year}) encontrado pero sin rating suficiente",
                            title, year);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "FA Sync: error procesando {Title}", title);
                skipped++;
            }

            // Reportar progreso
            progress.Report((double)(i + 1) / total * 100);

            // Log cada 50 items para tracking
            if ((i + 1) % 50 == 0)
            {
                _logger.LogInformation(
                    "FA Sync: {Processed}/{Total} procesados, {Updated} actualizados, {Skipped} omitidos",
                    i + 1, total, updated, skipped);
            }

            // Respetar el throttle configurado para no saturar FilmAffinity
            await Task.Delay(config.ThrottleMs, ct).ConfigureAwait(false);
        }

        _logger.LogInformation(
            "FA Sync completado: {Updated} actualizados / {Skipped} omitidos / {Total} total",
            updated, skipped, total);
    }

    /// <summary>
    /// Configura el trigger por defecto: ejecucion diaria a las 3 AM.
    /// El usuario puede cambiar la hora desde Dashboard > Tareas Programadas.
    /// </summary>
    public IEnumerable<TaskTriggerInfo> GetDefaultTriggers()
    {
        yield return new TaskTriggerInfo
        {
            Type = MediaBrowser.Model.Tasks.TaskTriggerInfoType.DailyTrigger,
            TimeOfDayTicks = TimeSpan.FromHours(3).Ticks
        };
    }
}
