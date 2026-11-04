# BE-00 — Base API, configuración y diagnóstico

**Fecha:** 2026-11-03
**Estado:** Implementado y verificado localmente; AWS/TLS exterior pendientes.
**IDs:** BE-02, BE-03, BE-11, BE-31, BE-32, BE-33

## 1. Contexto y objetivo

Host ASP.NET Core .NET 8 con composición explícita, configuración por entorno, middlewares de seguridad, MongoDB Atlas, Redis, JWT, moderación y almacenamiento. Esta base sostiene los siete endpoints de evaluación sin convertirse en un segundo contrato.

## 2. Requisitos funcionales

- RF1: la solución compila con API y siete proyectos de prueba; DI resuelve servicios y opciones.
- RF2: las opciones se leen de `appsettings.json`, `appsettings.{Environment}.json` y, opcionalmente, `appsettings.{Environment}.local.json` ignorado por Git.
- RF3: el pipeline HTTP aplica request id, errores seguros, cabeceras, rechazo de claves NoSQL, CORS, autenticación JWT, rate limit y autorización.
- RF4: `GET /health` es anónimo; Swagger solo se expone en Development/Testing.
- RF5: los errores usan envoltura segura con `code`, `message`, `request_id` y `details`; no incluyen stack traces ni PII.
- RF6: los secretos no se versionan; la configuración local de Atlas vive en un archivo ignorado.

## 3. Requisitos no funcionales

HTTPS en despliegue, HSTS en Production, cabeceras de seguridad, rate limit sobre Redis con fallback local, logs estructurados sin correos completos, tokens, contraseñas ni URLs firmadas. La conexión Atlas se verificó localmente con TLS e índices; AWS requiere allowlist de las IPs NAT.

## 4. Arquitectura y decisiones

| Ruta | Responsabilidad |
|---|---|
| `Pactogg.Api/Program.cs` | Host, configuración, DI, JWT, CORS, Swagger, middlewares y mapas |
| `Pactogg.Api/Infrastructure/Options/` | Opciones tipadas |
| `Pactogg.Api/Common/Middleware/` | Request id, errores, cabeceras, NoSQL y rate limit |
| `Pactogg.Api/Infrastructure/Mongo/` | Contexto, índices y repositorios |
| `Pactogg.Api/Infrastructure/Redis/` | Caché, revocación, intentos y rate limit |
| `Pactogg.Api/Infrastructure/Security/` | Argon2id y JWT |

- **BE00-D1:** .NET 8 LTS por directriz del proyecto.
- **BE00-D2:** Minimal APIs y controladores delgados; servicios de aplicación en módulos.
- **BE00-D3:** Configuración local opcional `appsettings.{Environment}.local.json`; no se versiona.
- **BE00-D4:** Swagger restringido a Development/Testing.
- **BE00-D5:** `/health` verifica el proceso, no MongoDB ni Redis; no se declara salud no registrada.
- **BE00-D6:** Redis usa fallback local por proceso si no está disponible; producción usa Redis en contenedor en la instancia 1, compartido por ambas instancias (F2-D19).

## 5. Criterios de aceptación

| ID | Escenario | Evidencia |
|---|---|---|
| BE00-AC1 | La solución compila en Release y los servicios resuelven | Build local 0 errores |
| BE00-AC2 | La configuración local de Atlas se carga sin estar en Git | `appsettings.Development.local.json` ignorado |
| BE00-AC3 | `/health` responde 200 sin autenticación | Prueba manual local |
| BE00-AC4 | Swagger no está disponible en Production | Revisión de `Program.cs`; pendiente prueba de entorno |
| BE00-AC5 | Errores y cabeceras no filtran trazas ni PII | SecurityTest |

## 6. Plan de pruebas

Build Release, pruebas unitarias/integración/seguridad/contrato, arranque local, `/health`, cabeceras, 401 sin token y rechazo de claves NoSQL. La conexión Atlas se verificó con TLS e índices; falta un flujo E2E de endpoints contra Atlas.

## 7. Riesgos y deuda

Atlas conecta e inicializa índices desde este entorno; falta E2E y allowlist de las IPs NAT en AWS. Redis local no está disponible. El HTTPS público lo da Caddy con Let's Encrypt (F2-D19); el smoke público se registra en `specs/validation.md`. Swagger en Production requiere prueba de regresión.

## Aprobación

| Estado | Revisores | Fecha | Detalle |
|---|---|---|---|
| Implementación local | Agente | 2026-11-03 | Build y pruebas |
| Aceptación externa | Pendiente | Pendiente | AWS/TLS; Atlas local verificado |

[Plan](plan.md), [tareas](tasks.md), [checklist](checklists/checklist.md).

