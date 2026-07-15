using HtmlAgilityPack;
using Microsoft.Extensions.Logging;
using Jellyfin.Plugin.FilmAffinity.Scraping;

namespace Jellyfin.Plugin.FilmAffinity.Api;

/// <summary>
/// Cliente de scraping nativo de FilmAffinity.
/// v2.0: Realiza scraping directo con HttpClient + HtmlAgilityPack,
/// sin dependencia de API externa ni Puppeteer.
/// La configuracion del HttpClient (BaseAddress, Timeout, User-Agent)
/// se aplica via PluginServiceRegistrator.
/// </summary>
public class FilmAffinityClient
{
    private readonly HttpClient _http;
    private readonly FilmAffinityCacheService _cache;
    private readonly ILogger<FilmAffinityClient> _logger;

    public FilmAffinityClient(
        HttpClient http,
        FilmAffinityCacheService cache,
        ILogger<FilmAffinityClient> logger)
    {
        _http = http;
        _cache = cache;
        _logger = logger;
    }

    /// <summary>
    /// Obtiene el rating de FA para una pelicula individual.
    /// Estrategia de matching en 3 pasos:
    /// 1. Titulo original + anio
    /// 2. Titulo localizado + anio (si es distinto del original)
    /// 3. Titulo original sin anio (fallback)
    /// </summary>
    public async Task<RatingResult?> GetMovie(
        string originalTitle, string? localizedTitle, int? year,
        CancellationToken ct = default)
    {
        // 1. Intentar cache primero
        if (_cache.TryGet(originalTitle, year, out var cached))
            return cached;

        // 2. Buscar con titulo original + anio
        var result = await SearchAndGetRating(originalTitle, year, ct)
            .ConfigureAwait(false);

        // 3. Fallback: titulo localizado + anio (si es distinto del original)
        if (result is null && localizedTitle is not null
            && !string.Equals(originalTitle, localizedTitle, StringComparison.OrdinalIgnoreCase))
        {
            result = await SearchAndGetRating(localizedTitle, year, ct)
                .ConfigureAwait(false);
        }

        // 4. Fallback: titulo original sin filtrar por anio
        if (result is null && year.HasValue)
        {
            result = await SearchAndGetRating(originalTitle, null, ct)
                .ConfigureAwait(false);
        }

        // 5. Guardar en cache para futuras consultas
        if (result is not null)
            _cache.Set(originalTitle, year, result);

        return result;
    }

    /// <summary>
    /// Flujo completo: busca en FilmAffinity y, si encuentra una ficha,
    /// extrae el rating de la pagina de detalle.
    /// </summary>
    /// <param name="title">Titulo a buscar.</param>
    /// <param name="year">Anio opcional para filtrar resultados.</param>
    /// <param name="ct">Token de cancelacion.</param>
    /// <returns>RatingResult si se encontro y extrajo el rating, null en caso contrario.</returns>
    private async Task<RatingResult?> SearchAndGetRating(
        string title, int? year, CancellationToken ct)
    {
        try
        {
            // 1. Buscar en FilmAffinity
            var searchUrl = await SearchAsync(title, year, ct).ConfigureAwait(false);
            if (searchUrl is null)
                return null;

            // 2. Extraer rating de la ficha
            return await GetRatingAsync(searchUrl, title, year, ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "FA scraping: error para {Title} ({Year})", title, year);
            return null;
        }
    }

    /// <summary>
    /// Busca una pelicula/serie en la pagina de busqueda de FilmAffinity
    /// y devuelve la URL de la ficha mas probable.
    /// </summary>
    /// <param name="title">Titulo a buscar.</param>
    /// <param name="year">Anio opcional para filtrar resultados.</param>
    /// <param name="ct">Token de cancelacion.</param>
    /// <returns>URL relativa de la ficha (ej: /es/film704416.html) o null.</returns>
    public async Task<string?> SearchAsync(string title, int? year, CancellationToken ct = default)
    {
        var normalizedTitle = FilmAffinityHelpers.NormalizeSearchText(title);
        if (string.IsNullOrEmpty(normalizedTitle))
            return null;

        var searchUrl = $"search.php?stext={Uri.EscapeDataString(normalizedTitle)}&stype=title";

        _logger.LogDebug("FA search: {Url}", searchUrl);

        string html;
        try
        {
            using var response = await _http.GetAsync(searchUrl, ct).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("FA search: HTTP {StatusCode} para {Title}",
                    (int)response.StatusCode, title);
                return null;
            }

            html = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "FA search: HTTP error para {Title}", title);
            return null;
        }

        // Deteccion de Cloudflare / anti-bot
        if (html.Contains("Verifique que usted es un ser humano",
                StringComparison.OrdinalIgnoreCase)
            || html.Contains("Just a moment",
                StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogCritical(
                "FA: Cloudflare challenge detectado. FilmAffinity esta bloqueando peticiones HTTP simples. " +
                "Considera cambiar el User-Agent o esperar a que el bloqueo se levante.");
            return null;
        }

        return ParseSearchResults(html, title, year);
    }

    /// <summary>
    /// Extrae la URL de la ficha mas probable de la pagina de resultados de busqueda.
    /// Busca enlaces con el patron /filmXXXXX.html dentro de contenedores de resultado.
    /// </summary>
    private string? ParseSearchResults(string html, string title, int? year)
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        // Obtener todos los enlaces que apunten a fichas de pelicula (/filmXXXXX.html)
        var anchors = doc.DocumentNode.SelectNodes("//a[contains(@href,'/film') and contains(@href,'.html')]");
        if (anchors is null || anchors.Count == 0)
        {
            _logger.LogDebug("FA search: sin resultados para {Title}", title);
            return null;
        }

        var yearStr = year?.ToString();
        var seen = new HashSet<string>();
        var candidates = new List<SearchResult>();

        foreach (var anchor in anchors)
        {
            var href = anchor.GetAttributeValue("href", "");
            if (string.IsNullOrEmpty(href) || !seen.Add(href))
                continue;

            // Filtrar solo enlaces de fichas de pelicula/serie
            if (!href.Contains("/film", StringComparison.OrdinalIgnoreCase))
                continue;

            // Determinar el titulo del candidato desde el texto del enlace
            var titleText = (anchor.InnerText ?? "").Replace("\n", " ").Replace("\r", " ").Trim();
            // Colapsar espacios multiples
            titleText = System.Text.RegularExpressions.Regex.Replace(titleText, @"\s+", " ");

            // Obtener texto de contexto (elemento contenedor cercano)
            var container = anchor.Ancestors("li").FirstOrDefault()
                ?? anchor.Ancestors("div").FirstOrDefault()
                ?? anchor.ParentNode;

            var contextText = container?.InnerText ?? titleText;
            contextText = System.Text.RegularExpressions.Regex.Replace(contextText, @"\s+", " ").Trim();

            var candidate = new SearchResult(titleText, href, contextText, 0);
            var score = SearchScorer.Score(candidate, title, yearStr);

            candidates.Add(candidate with { Score = score });
        }

        if (candidates.Count == 0)
        {
            _logger.LogDebug("FA search: sin candidatos validos para {Title}", title);
            return null;
        }

        // Seleccionar el mejor candidato por puntuacion
        var best = candidates.OrderByDescending(c => c.Score).First();

        _logger.LogDebug("FA search: best candidate {Title} ({Url}) score={Score}",
            best.Title, best.Url, best.Score);

        // Si la puntuacion es 0, no hay coincidencia significativa
        if (best.Score == 0)
        {
            _logger.LogDebug("FA search: candidato con score 0 para {Title}, descartando", title);
            return null;
        }

        return best.Url;
    }

    /// <summary>
    /// Extrae el rating, votos y datos de una ficha de FilmAffinity.
    /// Usa los selectores CSS equivalentes al scraper de Cruzadera.
    /// </summary>
    /// <param name="filmUrl">URL relativa de la ficha (ej: /es/film704416.html).</param>
    /// <param name="requestedTitle">Titulo original solicitado para verificacion.</param>
    /// <param name="requestedYear">Anio solicitado para verificacion.</param>
    /// <param name="ct">Token de cancelacion.</param>
    /// <returns>RatingResult con los datos extraidos, o null si no se encontro rating.</returns>
    public async Task<RatingResult?> GetRatingAsync(
        string filmUrl, string requestedTitle, int? requestedYear,
        CancellationToken ct = default)
    {
        // Asegurar URL absoluta
        var fullUrl = filmUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase)
            ? filmUrl
            : filmUrl.StartsWith('/')
                ? filmUrl
                : "/" + filmUrl;

        _logger.LogDebug("FA detail: {Url}", fullUrl);

        string html;
        try
        {
            using var response = await _http.GetAsync(fullUrl, ct).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("FA detail: HTTP {StatusCode} para {Url}",
                    (int)response.StatusCode, fullUrl);
                return null;
            }

            html = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "FA detail: HTTP error para {Url}", fullUrl);
            return null;
        }

        // Cloudflare check tambien en la pagina de detalle
        if (html.Contains("Verifique que usted es un ser humano",
                StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogCritical("FA: Cloudflare challenge en pagina de detalle");
            return null;
        }

        return ParseRatingPage(html, fullUrl, requestedTitle, requestedYear);
    }

    /// <summary>
    /// Parsea la pagina de detalle de FilmAffinity para extraer rating, votos y titulo.
    /// Selectores CSS (equivalentes al scraper de Cruzadera):
    /// - Rating: #rat-avg-count, #movie-rat-avg, meta[itemprop="ratingValue"]
    /// - Votos: #movie-count-rat, meta[itemprop="ratingCount"]
    /// - Titulo: #main-title
    /// - Anio: .fa-year, dd[itemprop="datePublished"]
    /// </summary>
    private RatingResult? ParseRatingPage(
        string html, string url, string requestedTitle, int? requestedYear)
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        // --- Extraer titulo ---
        var titleNode = doc.DocumentNode.SelectSingleNode("//*[@id='main-title']");
        var title = titleNode?.InnerText?.Trim()
            ?? doc.DocumentNode.SelectSingleNode("//h1")?.InnerText?.Trim()
            ?? requestedTitle;

        // --- Extraer rating ---
        // Selectores en orden de preferencia
        var ratingStr =
            GetText(doc, "#rat-avg-count")
            ?? GetText(doc, "#movie-rat-avg")
            ?? GetAttr(doc, "//meta[@itemprop='ratingValue']", "content")
            ?? "";

        // FilmAffinity usa coma decimal en espaniol: "7,9" -> 7.9
        ratingStr = ratingStr.Trim();
        float? rating = null;
        if (!string.IsNullOrEmpty(ratingStr))
        {
            ratingStr = ratingStr.Replace(",", ".");
            if (float.TryParse(ratingStr,
                    System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out var r))
            {
                rating = r;
            }
        }

        if (rating is null || rating.Value <= 0)
        {
            _logger.LogDebug("FA detail: sin rating en {Url}", url);
            return null;
        }

        // --- Extraer votos ---
        var votesStr =
            GetText(doc, "#movie-count-rat")
            ?? GetAttr(doc, "//meta[@itemprop='ratingCount']", "content")
            ?? "";

        var votes = FilmAffinityHelpers.ParseVotes(votesStr);

        // --- Extraer anio ---
        var yearStr =
            GetText(doc, ".fa-year")
            ?? GetText(doc, "dd[itemprop='datePublished']")
            ?? GetAttr(doc, "//meta[@itemprop='datePublished']", "content");

        // Si no se encontro con selectores, buscar patron 19xx/20xx
        // SOLO en el area de ficha tecnica, no en todo el documento
        if (string.IsNullOrEmpty(yearStr))
        {
            var detailsNode = doc.DocumentNode.SelectSingleNode("//*[@id='movie-main-details']")
                ?? doc.DocumentNode.SelectSingleNode("//dl[@class='movie-info']")
                ?? doc.DocumentNode;

            var bodyText = detailsNode.InnerText;
            var yearMatch = System.Text.RegularExpressions.Regex.Match(
                bodyText, @"\b(19|20)\d{2}\b");
            yearStr = yearMatch.Success ? yearMatch.Value : null;
        }

        // Verificar coincidencia de anio si se solicito
        if (requestedYear.HasValue && yearStr is not null
            && int.TryParse(yearStr, out var parsedYear))
        {
            if (Math.Abs(parsedYear - requestedYear.Value) > 1)
            {
                _logger.LogDebug(
                    "FA detail: mismatch de anio para {Title}: esperado {Req}, obtenido {Got}",
                    title, requestedYear, yearStr);
                return null;
            }
        }

        _logger.LogDebug(
            "FA detail: {Title} ({Year}) -> {Rating}/10 [{Votes} votos]",
            title, yearStr, rating.Value, votes);

        return new RatingResult(title, yearStr, rating.Value, votes, url);
    }

    /// <summary>
    /// Obtiene el texto de un nodo mediante selector CSS (via XPath).
    /// Soporta selectores basicos: #id, .class, tag, [attr=value].
    /// </summary>
    private static string? GetText(HtmlDocument doc, string cssSelector)
    {
        var xpath = CssToXPath(cssSelector);
        var node = doc.DocumentNode.SelectSingleNode(xpath);
        return node?.InnerText?.Trim();
    }

    /// <summary>
    /// Obtiene el valor de un atributo de un nodo.
    /// </summary>
    private static string? GetAttr(HtmlDocument doc, string xpath, string attr)
    {
        var node = doc.DocumentNode.SelectSingleNode(xpath);
        return node?.GetAttributeValue(attr, string.Empty);
    }

    /// <summary>
    /// Convierte selectores CSS basicos a XPath.
    /// Soporta: #id, .class, tag, [attr=value], combinaciones simples.
    /// </summary>
    private static string CssToXPath(string css)
    {
        var xpath = css.Trim();

        // #id -> //*[@id='id']
        if (xpath.StartsWith('#'))
        {
            var id = xpath[1..];
            // Por si tiene sufijos como .fa-year (clase adicional)
            var spaceIdx = id.IndexOfAny([' ', '.', '[']);
            if (spaceIdx > 0)
            {
                var mainId = id[..spaceIdx];
                var rest = id[spaceIdx..];
                return $"//*[@id='{mainId}']{RestToXPath(rest)}";
            }
            return $"//*[@id='{id}']";
        }

        // .class -> //*[contains(@class,'class')]
        if (xpath.StartsWith('.'))
        {
            var cls = xpath[1..];
            var spaceIdx = cls.IndexOfAny([' ', '.', '[']);
            if (spaceIdx > 0)
            {
                var mainCls = cls[..spaceIdx];
                var rest = cls[spaceIdx..];
                return $"//*[contains(@class,'{mainCls}')]{RestToXPath(rest)}";
            }
            return $"//*[contains(@class,'{cls}')]";
        }

        // tag
        if (char.IsLetter(xpath[0]))
        {
            var tagEnd = 0;
            for (var i = 0; i < xpath.Length; i++)
            {
                if (!char.IsLetterOrDigit(xpath[i]) && xpath[i] != '-')
                {
                    tagEnd = i;
                    break;
                }
            }
            if (tagEnd == 0) tagEnd = xpath.Length;
            var tag = xpath[..tagEnd];
            var rest = xpath[tagEnd..];
            return $"//{tag}{RestToXPath(rest)}";
        }

        return $"//*{RestToXPath(xpath)}";
    }

    /// <summary>
    /// Convierte el resto de un selector CSS (clases, atributos) a predicados XPath.
    /// </summary>
    private static string RestToXPath(string rest)
    {
        if (string.IsNullOrEmpty(rest))
            return string.Empty;

        var result = new System.Text.StringBuilder();

        // [attr=value]
        var attrMatch = System.Text.RegularExpressions.Regex.Match(
            rest, @"\[@?(?<attr>\w+)\s*=\s*['""](?<value>[^'""]*)['""]\]");
        if (attrMatch.Success)
        {
            result.Append($"[@{attrMatch.Groups["attr"].Value}='{attrMatch.Groups["value"].Value}']");
        }

        return result.ToString();
    }
}
