# Jellyfin Plugin: FilmAffinity Ratings

Plugin nativo para [Jellyfin](https://jellyfin.org/) que integra las valoraciones de [FilmAffinity](https://www.filmaffinity.com/) como metadato complementario en tu biblioteca. Scraping directo con `HttpClient` + `HtmlAgilityPack`, sin dependencias externas.

## Funcionalidades

* Añade el rating de FilmAffinity al campo `CommunityRating` de Jellyfin.
* Soporte para películas, series y sagas/colecciones.
* Sincronización masiva programada para *backfill* de bibliotecas existentes.
* Scraping nativo autocontenido: sin API companion, sin Node.js, sin Docker, sin servicios externos.
* Caché inteligente en memoria: 6 h para películas recientes (menos de 2 años) y 24 h para el resto. Límite de 10 000 entradas.
* Página de configuración integrada en el dashboard de Jellyfin.
* Detección automática de bloqueos Cloudflare con aviso en logs.
* Los ítems procesados se marcan con `ProviderId["FilmAffinity"]` y no se reprocesan en ejecuciones posteriores.

## Requisitos

* Jellyfin 10.11.x o superior.
* .NET 9.0 Runtime (incluido en Jellyfin 10.11.x).
* Acceso a internet desde el servidor Jellyfin para consultar `filmaffinity.com`.

## Instalación

### Desde el catálogo de Jellyfin (recomendado)

1. Ve a **Dashboard > Plugins > Repositorios**.
2. Añade el manifiesto: `https://raw.githubusercontent.com/falbuixech/jellyfin-plugin-filmaffinity/main/manifest.json`
3. Ve a **Catálogo** y busca "FilmAffinity Ratings".
4. Instala y reinicia Jellyfin.

### Instalación manual

1. Descarga `Jellyfin.Plugin.FilmAffinity.zip` de la [última release](https://github.com/falbuixech/jellyfin-plugin-filmaffinity/releases).
2. Extrae el .dll en la carpeta de plugins de Jellyfin:

   * **Windows:** `%LOCALAPPDATA%\jellyfin\plugins\FilmAffinity\`
   * **Linux:** `/var/lib/jellyfin/plugins/FilmAffinity/`
   * **Docker:** monta el volumen en `/config/plugins/FilmAffinity/`
3. Reinicia Jellyfin.

Tras la instalación, ejecuta **Dashboard > Tareas Programadas > Sincronizar valoraciones de FilmAffinity** para procesar tu biblioteca existente.

## Configuración

Accede desde **Dashboard > Plugins > FilmAffinity Ratings**.

| Parámetro | Por defecto | Descripción |
|---|---|---|
| `FilmAffinityBaseUrl` | `https://www.filmaffinity.com/es/` | URL base de FilmAffinity (incluye idioma) |
| `RequestTimeoutSeconds` | `30` | Timeout HTTP por request |
| `UserAgent` | Chrome 131 / Windows | User-Agent enviado a FilmAffinity |
| `MinVotes` | `50` | Mínimo de votos para considerar la valoración fiable |
| `SetCriticRating` | `false` | También escribir `CriticRating` (FA rating x 10) |
| `ThrottleMs` | `2000` | Delay entre peticiones HTTP consecutivas |

## Cómo funciona

### Al añadir nuevo contenido (Provider)

Cuando Jellyfin escanea una nueva película o serie, el provider consulta FilmAffinity directamente con esta estrategia:

1. Busca en FilmAffinity por título original + año.
2. Si no encuentra, reintenta con el título en español + año.
3. Si sigue sin encontrar, busca solo por título original (sin año).

Si obtiene un resultado con suficientes votos, escribe el rating en `CommunityRating` y marca el ítem con `ProviderId["FilmAffinity"]`.

### Bibliotecas existentes (SyncTask)

La tarea programada `Sincronizar valoraciones de FilmAffinity` se ejecuta diariamente a las 3:00 AM. Recorre secuencialmente todos los ítems sin `ProviderId` de FilmAffinity, buscando cada uno individualmente en `filmaffinity.com` con un retardo configurable entre peticiones.

Los ítems procesados (con o sin rating) se marcan para no reintentarse en futuras ejecuciones.

### Scraping

El plugin realiza scraping directo del HTML de FilmAffinity usando `HtmlAgilityPack`. No utiliza Puppeteer, ni navegadores headless, ni APIs intermedias. Los selectores CSS son equivalentes al scraper de referencia de [Cruzadera](https://github.com/Cruzadera/filmaffinity-scores).

Si FilmAffinity activa protección Cloudflare, el plugin lo detecta y registra un aviso `CRITICAL` en los logs de Jellyfin sin interrumpir el funcionamiento del servidor.

## Desarrollo

```bash
git clone https://github.com/falbuixech/jellyfin-plugin-filmaffinity.git
cd jellyfin-plugin-filmaffinity
dotnet restore
dotnet build
```

## Arquitectura

Ver [docs/architecture.md](docs/architecture.md) para el diagrama de componentes, las decisiones de diseño (ADR) y los diagramas de secuencia.

## Contribuir

Ver [CONTRIBUTING.md](CONTRIBUTING.md).

## Autor

Desarrollado por [falbuixech](https://github.com/falbuixech).

## Licencia

GPLv3. Ver [LICENSE](LICENSE).
