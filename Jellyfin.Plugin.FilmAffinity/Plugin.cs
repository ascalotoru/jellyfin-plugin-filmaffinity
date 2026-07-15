using MediaBrowser.Common.Plugins;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;
using Microsoft.Extensions.Logging;
using Jellyfin.Plugin.FilmAffinity.Configuration;

namespace Jellyfin.Plugin.FilmAffinity;

/// <summary>
/// Plugin principal de FilmAffinity para Jellyfin.
/// Registra los providers de metadata y la tarea programada de sincronizacion.
/// </summary>
public class Plugin : BasePlugin<PluginConfiguration>, IHasWebPages
{
    public const string ProviderName = "FilmAffinity";
    public const string ProviderId = "FilmAffinity";

    public override string Name => ProviderName;
    public override Guid Id => Guid.Parse("A7B8C9D0-E1F2-3456-7890-ABCDEF123456");

    /// <summary>Instancia unica del plugin accesible desde cualquier parte del codigo.</summary>
    public static Plugin? Instance { get; private set; }

    /// <summary>Logger estatico para uso en clases que no reciben DI.</summary>
    public static ILogger<Plugin>? Log { get; private set; }

    public Plugin(
        IApplicationPaths applicationPaths,
        IXmlSerializer xmlSerializer,
        ILogger<Plugin> logger)
        : base(applicationPaths, xmlSerializer)
    {
        Instance = this;
        Log = logger;
    }

    /// <summary>
    /// Paginas web del plugin. La pagina de configuracion se sirve como recurso embebido.
    /// </summary>
    public IEnumerable<PluginPageInfo> GetPages()
    {
        yield return new PluginPageInfo
        {
            Name = Name,
            EmbeddedResourcePath =
                $"{GetType().Namespace}.Configuration.config.html"
        };
    }
}
