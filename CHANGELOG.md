# Changelog

Todas las versiones notables de este proyecto se documentan en este archivo.

El formato sigue las recomendaciones de [Keep a Changelog](https://keepachangelog.com/),
y este proyecto adhiere a [Semantic Versioning](https://semver.org/lang/es/).

---

## [2.0.1] — 2026-10-17

### Corregido

- **Empaquetado**: el zip de la release solo incluía `Jellyfin.Plugin.FilmAffinity.dll`.
  Faltaban `HtmlAgilityPack.dll` y `Jellyfin.Plugin.FilmAffinity.deps.json`, por lo que
  el plugin fallaba con `FileNotFoundException: HtmlAgilityPack` en cada ítem (búsqueda
  y parseo). Ahora `dotnet publish` incluye la dependencia y `build.yaml` la empaqueta.
- **Extracción de rating/votos**: se probaba `#rat-avg-count` (contenedor externo que
  incluye votos y texto) antes que `#movie-rat-avg`, y el operador `??` cortocircuitaba
  la cadena, por lo que el rating nunca se leía. Ahora se lee el atributo `content` de
  `#movie-rat-avg` (`itemprop="ratingValue"`) y los votos de `itemprop="ratingCount"`.
  Sin esto, la sincronización terminaba con `0 actualizados`.
- **Caché**: `PluginServiceRegistrator` fijaba `MemoryCacheOptions.SizeLimit` en la
  `IMemoryCache` compartida de Jellyfin, obligando a que TODAS las entradas (también las
  de Jellyfin/TMDb) declarasen `Size` y provocando errores
  `Cache entry must specify a value for Size when SizeLimit is set`. Se elimina el límite.

---

## [2.0.0] — 2026-07-15

### Cambiado

- **Scraping nativo**: el plugin ahora consulta FilmAffinity directamente mediante
  `HttpClient` + `HtmlAgilityPack`, eliminando la dependencia de la API externa
  `filmaffinity-scores-api`. Sin Node.js, sin Puppeteer, sin Docker, sin servicios
  externos. El plugin es completamente autocontenido.
- `FilmAffinityClient` reescrito: ahora implementa búsqueda (`SearchAsync`) y
  extracción de rating (`GetRatingAsync`) mediante scraping del HTML de FilmAffinity.
- `FilmAffinitySyncTask` reescrito: procesamiento secuencial de ítems con scraping
  directo en lugar de API batch externa.
- Configuración renombrada: `ApiBaseUrl` → `FilmAffinityBaseUrl`,
  `ApiTimeoutSeconds` → `RequestTimeoutSeconds`. Añadido `UserAgent` configurable.
- Selectores CSS equivalentes al scraper de referencia de Cruzadera.

### Eliminado

- Dependencia de `filmaffinity-scores-api` (Node.js, Puppeteer, Chromium).
- `BatchSize`: el procesamiento secuencial no requiere lotes.
- `Api/Models/BatchRequest.cs`, `BatchResponse.cs`, `MovieResult.cs` (modelos de API externa).

### Añadido

- `Scraping/ScraperModels.cs`: registros `SearchResult` y `RatingResult`.
- `Scraping/SearchScorer.cs`: lógica de puntuación de candidatos de búsqueda
  (replica el algoritmo del scraper de Cruzadera).
- `HtmlAgilityPack` 1.12.1 como dependencia NuGet (licencia MIT, 0 dependencias transitivas).
- `NormalizeSearchText()` en `FilmAffinityHelpers`: normalización Unicode NFD
  para comparación de títulos.
- Detección de Cloudflare challenge en `SearchAsync` y `GetRatingAsync` con
  registro `CRITICAL` en logs.
- Fallback de año con `meta[@itemprop='datePublished']` y búsqueda limitada
  al área de ficha técnica.
- TTL corto (6h) como fallback en caché cuando el año no es parseable.

### Seguridad

- `HttpResponseMessage` dispuesto correctamente con `using var` en todas las
  peticiones HTTP.
- Ítems sin rating se marcan con `ProviderId = "searched"` para evitar
  reintentos perpetuos.

---

## [1.0.0] — 2026-07-15

### Añadido

- `FilmAffinityMovieProvider`: provider nativo que añade el rating de FilmAffinity
  al campo `CommunityRating` de películas.
- `FilmAffinitySeriesProvider`: mismo comportamiento para series de TV.
- `FilmAffinityBoxSetProvider`: mismo comportamiento para sagas y colecciones (BoxSet).
- `FilmAffinitySyncTask`: tarea programada que recorre la biblioteca en busca de
  ítems sin ProviderId de FilmAffinity y los sincroniza vía la API batch.
- `FilmAffinityClient`: cliente HTTP para la API REST de `filmaffinity-scores`
  con estrategia de matching en 3 pasos (título original, localizado, sin año).
- `FilmAffinityCacheService`: caché en memoria con TTL inteligente (6h para
  películas recientes, 24h para el resto) y límite de 10 000 entradas.
- `FilmAffinityHelpers`: métodos auxiliares `ParseVotes` y `HasEnoughVotes`
  compartidos entre providers y el SyncTask.
- Página de configuración integrada en el dashboard de Jellyfin (HTML embebido).
- `PluginServiceRegistrator` con registro DI de HttpClient, MemoryCache y servicios.
- `build.yaml` con manifiesto JPRM listo para distribución.

### Dependencias

- Jellyfin 10.11.8+ (Controller + Model)
- .NET 9.0
- Microsoft.Extensions.Http 9.0.11
- Microsoft.Extensions.Caching.Memory 9.0.11
- `filmaffinity-scores-api` (Cruzadera, companion externo en localhost:8085)

---

[2.0.0]: https://github.com/falbuixech/jellyfin-plugin-filmaffinity/releases/tag/v2.0.0
[1.0.0]: https://github.com/falbuixech/jellyfin-plugin-filmaffinity/releases/tag/v1.0.0
