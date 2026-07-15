# Guia de Contribucion — jellyfin-plugin-filmaffinity

Gracias por tu interes en contribuir. Este documento describe el flujo de
trabajo y las convenciones del proyecto.

## Flujo de Trabajo

1. Haz un fork del repositorio en GitHub.
2. Crea una rama con el prefijo adecuado:
   - `feat/` para nuevas funcionalidades
   - `fix/` para correcciones de errores
   - `docs/` para cambios en documentacion
3. Implementa tus cambios siguiendo las convenciones de codigo.
4. Asegurate de que el proyecto compila sin errores: `dotnet build`
5. Abre un Pull Request contra la rama `main`.

## Convenciones de Codigo

### Idioma

- Comentarios y documentacion: **espanol**.
- Nombres de clases, metodos, variables, namespaces: **ingles** (convencion
  del ecosistema .NET).
- Mensajes de commit: **espanol**.

### Estilo C#

- Seguir las [convenciones de codigo de .NET](https://learn.microsoft.com/es-es/dotnet/csharp/fundamentals/coding-style/coding-conventions).
- Usar `PascalCase` para clases, metodos, propiedades.
- Usar `camelCase` para campos privados con prefijo `_` (ej: `_logger`).
- Documentar todos los miembros publicos con `/// <summary>`.
- Usar `ConfigureAwait(false)` en todos los `await` dentro de codigo de
  biblioteca.

### Estructura del Proyecto

```
Jellyfin.Plugin.FilmAffinity/
├── Api/            # Cliente HTTP de scraping (FilmAffinityClient) y cache (CacheService)
├── Configuration/  # Configuracion del plugin y pagina HTML del dashboard
├── Providers/      # IRemoteMetadataProvider (Movie, Series, BoxSet)
├── Scraping/       # Modelos y logica de scraping (ScraperModels, SearchScorer)
├── ScheduledTasks/ # IScheduledTask (FilmAffinitySyncTask)
├── Plugin.cs       # Punto de entrada del plugin
├── PluginServiceRegistrator.cs  # Registro de servicios en DI
└── FilmAffinityHelpers.cs       # Metodos auxiliares compartidos
```

## Pull Requests

- El titulo debe describir el cambio en espanol.
- La descripcion debe incluir: que se cambio, por que, y como probarlo.
- Si el PR resuelve un issue, referenciarlo con `Closes #XX`.
- Todos los PRs deben compilar sin errores.
- Los PRs que modifiquen el comportamiento deben actualizar `CHANGELOG.md`.

## Pruebas

Actualmente el proyecto no tiene tests automatizados. Si anades tests:

- Usa `xUnit` como framework de pruebas.
- Crea un proyecto `Jellyfin.Plugin.FilmAffinity.Tests`.
- Usa `Moq` para mockear dependencias.
- Prioriza testear: `FilmAffinityClient`, `CacheService`, `Helpers`.

## Reportar Errores

Abre un issue en GitHub con:

- Version del plugin y de Jellyfin.
- Pasos para reproducir el error.
- Comportamiento esperado vs. real.
- Logs relevantes (nivel Debug del plugin).

## Licencia

Al contribuir, aceptas que tu codigo se distribuya bajo la licencia GPLv3
del proyecto.
