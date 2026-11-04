# AUTH-04 — POST /api/v1/auth/logout

**Fecha:** 2026-11-03
**Estado:** Implementado y verificado localmente; aceptación externa pendiente.
**IDs:** AUTH-04, BE-11, BE-31
**Contrato:** `api/openapi.yaml`

## 1. Contexto y objetivo

Revocar el access token actual y, opcionalmente, el refresh token recibido o todos los refresh del usuario.

## 2. Requisitos funcionales

- RF1: exigir access token válido.
- RF2: revocar el `jti` actual en Redis hasta su `exp`.
- RF3: si `all=true`, revocar todos los refresh tokens del usuario.
- RF4: si se envía `refresh_token`, revocarlo solo si pertenece al usuario.
- RF5: responder `204 No Content` y auditar `auth.logout`.
- RF6: token ausente, inválido o revocado devuelve `401`.

## 3. Requisitos no funcionales

No registrar tokens. La revocación debe sobrevivir hasta la expiración natural. Redis es obligatorio en producción; fallback local en desarrollo.

## 4. Contrato

### Request

Requiere `Authorization: Bearer <access>`. Cuerpo opcional:

```json
{
  "refresh_token": "…",
  "all": false
}
```

### Response

`204 No Content` sin cuerpo.

### Errores

| Código HTTP | `code` | Causa |
|---|---|---|
| 401 | `UNAUTHORIZED` | Token ausente, inválido o revocado |
| 400 | `INVALID_INPUT` | JSON inválido |
| 500 | `INTERNAL_ERROR` | Fallo inesperado seguro |

## 5. Arquitectura y decisiones

| Ruta | Responsabilidad |
|---|---|
| `Pactogg.Api/Modules/Auth/AuthEndpoints.cs` | Ruta `POST /logout` |
| `Pactogg.Api/Modules/Auth/AuthService.cs` | `LogoutAsync` |
| `Pactogg.Api/Infrastructure/Redis/RedisTokenRevocationStore.cs` | `jti` revocado |
| `Pactogg.Api/Infrastructure/Mongo/Repositories/MongoRefreshTokenRepository.cs` | Refresh |

- **AUTH04-D1:** `204` por decisión D-19.
- **AUTH04-D2:** revocación global por `all=true`; no se implementa corte temporal de access tokens además del `jti` actual.

## 6. Criterios de aceptación

| ID | Escenario | Evidencia |
|---|---|---|
| AUTH04-AC1 | Logout válido devuelve 204 | Flujo de integración |
| AUTH04-AC2 | Token revocado deja de servir | `afterLogout` 401 |
| AUTH04-AC3 | `all=true` revoca refresh | Código; falta prueba dedicada |
| AUTH04-AC4 | Token inválido devuelve 401 | SecurityTest |
| AUTH04-AC5 | No se registran tokens | Revisión de logs |

## 7. Plan de pruebas

Flujo de logout y petición posterior con 401. Falta prueba de `all=true` y de refresh revocado.

## 8. Riesgos y deuda

El access token actual se revoca, pero no hay corte global de todos los access tokens. Redis real no probado. Falta prueba de `all=true`.

## Aprobación

| Estado | Revisores | Fecha | Detalle |
|---|---|---|---|
| Implementación local | Agente | 2026-11-03 | Build y pruebas |
| Aceptación externa | Pendiente | Pendiente | AWS/TLS/revisión |

[Plan](plan.md), [tareas](tasks.md), [checklist](checklists/checklist.md).

