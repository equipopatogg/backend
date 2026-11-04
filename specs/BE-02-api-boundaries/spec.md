# BE-02 — Fronteras HTTP, DTO y errores

**Fecha:** 2026-11-03
**Estado:** Implementado y verificado localmente.
**IDs:** BE-02, BE-11, BE-12, BE-13, BE-14

## 1. Contexto y objetivo

Centralizar el contrato HTTP: JSON snake_case, rechazo de campos desconocidos, envoltura `data`/`meta`, errores seguros, validación y autorización. Los endpoints consumen estas convenciones sin duplicarlas.

## 2. Requisitos funcionales

- RF1: request/response en snake_case; miembros desconocidos rechazados.
- RF2: éxitos con `data` y `meta` cuando aplique; errores con `code`, `message`, `request_id` y `details`.
- RF3: validación con DataAnnotations y filtros; entrada inválida no persiste.
- RF4: autenticación JWT con `sub`, `jti`, `iat`, `iss`, `aud`; revocación Redis.
- RF5: autorización por recurso/grupo; sin confiar en `group_id` del cliente.
- RF6: claves NoSQL `$` o `.` rechazadas.

## 3. Requisitos no funcionales

Sin stack traces, PII ni tokens en logs. CORS configurable. Rate limit por IP/usuario. Respuestas seguras en 401/403/404/409/422/429/500.

## 4. Arquitectura y decisiones

| Ruta | Responsabilidad |
|---|---|
| `Pactogg.Api/Common/ApiResults.cs` | Envoltura de éxito |
| `Pactogg.Api/Common/ApiException.cs` | Errores de aplicación |
| `Pactogg.Api/Common/ErrorCodes.cs` | Códigos estables |
| `Pactogg.Api/Common/ValidationFilter.cs` | Validación de endpoints |
| `Pactogg.Api/Common/Middleware/NoSqlKeyValidationMiddleware.cs` | Rechazo NoSQL |
| `Pactogg.Api/Common/Extensions/ClaimsPrincipalExtensions.cs` | `sub` a ObjectId |

- **BE02-D1:** snake_case con `JsonNamingPolicy.SnakeCaseLower`.
- **BE02-D2:** `UnmappedMemberHandling.Disallow`.
- **BE02-D3:** errores genéricos; detalles solo de validación.
- **BE02-D4:** OpenAPI versionado como contrato único.

## 5. Criterios de aceptación

| ID | Escenario | Evidencia |
|---|---|---|
| BE02-AC1 | Campo desconocido retorna 400 | `Request_WithUnknownField_Returns400` |
| BE02-AC2 | Claves NoSQL se rechazan | `Evidence_RejectsDangerousNoSqlKeys` |
| BE02-AC3 | 401/403 no filtran trazas | SecurityTest |
| BE02-AC4 | Respuestas usan `data` y snake_case | IntegrationTest/ContractTest |
| BE02-AC5 | OpenAPI documenta los endpoints | ContractTest |

## 6. Plan de pruebas

Pruebas de campo desconocido, claves NoSQL, token ausente/inválido, 403 por grupo, formatos de error, cabeceras y contrato OpenAPI. La semántica de negocio se valida en cada endpoint.

## 7. Riesgos y deuda

La envoltura de health es texto plano. Algunos errores de infraestructura pueden mapearse a 500 genérico. La compatibilidad Android no se ha probado.

## Aprobación

| Estado | Revisores | Fecha | Detalle |
|---|---|---|---|
| Implementación local | Agente | 2026-11-03 | Pruebas |
| Aceptación Android | Pendiente | Pendiente | Contrato consumidor |

[Plan](plan.md), [tareas](tasks.md), [checklist](checklists/checklist.md).

