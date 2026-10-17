using MediaBrowser.Controller.Plugins;
using MediaBrowser.Controller;
using Microsoft.Extensions.DependencyInjection;
using Jellyfin.Plugin.FilmAffinity.Api;

namespace Jellyfin.Plugin.FilmAffinity;

/// <summary>
/// Registra los servicios del plugin en el contenedor de DI de Jellyfin.
/// Jellyfin detecta automaticamente esta clase al iniciar y llama a RegisterServices.
/// v2.0: Scraping nativo, sin API externa. HttpClient apunta directamente a FilmAffinity.
/// </summary>
public class PluginServiceRegistrator : IPluginServiceRegistrator
{
    public void RegisterServices(IServiceCollection services, IServerApplicationHost host)
    {
        // HttpClient tipado para FilmAffinityClient.
        // La URL base y el timeout se configuran desde PluginConfiguration.
        // El User-Agent se configura explicitamente para evitar bloqueos.
        services.AddHttpClient<FilmAffinityClient>(client =>
        {
            var config = Plugin.Instance?.Configuration;
            client.BaseAddress = config is not null
                ? new Uri(config.FilmAffinityBaseUrl)
                : new Uri("https://www.filmaffinity.com/es/");

            client.Timeout = config is not null
                ? TimeSpan.FromSeconds(config.RequestTimeoutSeconds)
                : TimeSpan.FromSeconds(30);

            client.DefaultRequestHeaders.Clear();
            client.DefaultRequestHeaders.Add("User-Agent",
                config?.UserAgent ?? "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0.0.0 Safari/537.36");
            client.DefaultRequestHeaders.Add("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");
            client.DefaultRequestHeaders.Add("Accept-Language", "es-ES,es;q=0.9,en;q=0.8");
        });

        // Cache en memoria para resultados de FilmAffinity.
        // Evita consultas repetidas a FilmAffinity en intervalos cortos.
        // NO se fija SizeLimit: IMemoryCache es compartida por todo Jellyfin,
        // y un limite global obliga a que TODAS las entradas (tambien las de
        // Jellyfin u otros plugins) declaren Size, lo que provoca errores del
        // tipo "Cache entry must specify a value for Size when SizeLimit is set".
        // La cache del plugin acota su tamano por TTL.
        services.AddMemoryCache();
        services.AddSingleton<FilmAffinityCacheService>();
    }
}
