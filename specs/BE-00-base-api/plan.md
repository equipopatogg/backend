# Plan — BE-00

[Spec](spec.md).

## Enfoque

Host ASP.NET Core .NET 8 con configuración por capas, DI explícita, middlewares ordenados y adaptadores de infraestructura. La configuración local sensible se carga desde un archivo ignorado.

## Archivos

| Ruta | Cambio |
|---|---|
| `Pactogg.Api/Program.cs` | Carga local, JWT, CORS, Swagger por entorno, middlewares y mapas |
| `Pactogg.Api/Infrastructure/Options/` | Opciones tipadas |
| `Pactogg.Api/Common/Middleware/` | Seguridad y errores |
| `Pactogg.Api/Infrastructure/Mongo/` | Contexto e índices |
| `Pactogg.Api/Infrastructure/Redis/` | Caché y revocación |
| `Pactogg.Api/Infrastructure/Security/` | Hash y tokens |

## Dependencias

MongoDB.Driver, StackExchange.Redis, Serilog, JWT Bearer, Swashbuckle, AWS SDK y SkiaSharp. Sin ORM ni bases relacionales.

## Pruebas

Build, health, cabeceras, 401, claves NoSQL, opciones locales y pruebas de endpoints. La conexión Atlas se valida con allowlist y cluster activo.

## Riesgos

Cluster Atlas pausado o IP no permitida; Redis ausente; Swagger expuesto por error; secretos en archivos versionados.

## Orden

1. Configuración y DI.
2. Middlewares y seguridad.
3. Mongo/Redis y salud.
4. Pruebas y documentación.

## Cierre

Checklist, `Memoria.md` y `specs/validation.md` conciliados.

