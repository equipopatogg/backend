# USR-06 — GET /api/v1/users/check-nickname

**Fecha:** 2026-11-04
**Estado:** Implementado y verificado localmente (pruebas de integración en verde); despliegue con `scripts/release.sh`.
**Contrato:** `api/openapi.yaml` (v0.5.0)

## 1. Objetivo

Indicar si un apodo está libre durante el registro.

## 2. Requisitos funcionales

- RF1: público (AllowAnonymous), sin caché.
- RF2: valida `^[A-Za-z0-9_.-]{3,24}$`; inválido → 422. El Excel dice 3–20; se usa 3–24 para coincidir con el registro (D-15).
- RF3: respuesta `{nickname, available}`; no revela nada del dueño.
- RF4: límite estricto: 20 consultas por minuto por IP (`RateLimit:NicknameCheck*`) → 429.

## 3. Errores

| HTTP | `code` | Causa |
|---|---|---|
| 422 | `VALIDATION_ERROR` | Formato inválido |
| 429 | `RATE_LIMITED` | Límite superado |

## 4. Implementación

- `Pactogg.Api/Modules/Users/UserEndpoints.cs`
- `Pactogg.Api/Common/Middleware/RedisRateLimitMiddleware.cs (política nickname)`

## 5. Criterios de aceptación

| ID | Escenario | Evidencia |
|---|---|---|
| USR06-AC1 | Apodo existente (otra capitalización) → available false | SocialFlowTests |
| USR06-AC2 | Apodo libre → true; 'a' → 422 | SocialFlowTests |

Pruebas: `IntegrationTest/SocialFlowTests.cs` (fakes en memoria) y `SecurityTest/AuthorizationTests.cs` (401).
