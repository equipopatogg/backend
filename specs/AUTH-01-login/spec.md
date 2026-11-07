# AUTH-01 — POST /api/v1/auth/login

**Fecha:** 2026-11-03
**Estado:** Implementado y verificado localmente; aceptación externa pendiente.
**IDs:** AUTH-01, BE-11, BE-13
**Contrato:** `api/openapi.yaml`

## 1. Contexto y objetivo

Autenticar credenciales y emitir una sesión JWT, con bloqueo tras fallos consecutivos y respuesta genérica.

## 2. Requisitos funcionales

- RF1: aceptar `email`, `password` y `device_id` opcional.
- RF2: credenciales válidas devuelven `200` con access/refresh token.
- RF3: credenciales inválidas devuelven `401 INVALID_CREDENTIALS` con mensaje genérico.
- RF4: tras 5 fallos para el mismo correo e IP, bloquear 15 minutos y responder `429`.
- RF5: limpiar fallos tras login válido y actualizar `last_login_at`.
- RF6: auditar `auth.login`.

## 3. Requisitos no funcionales

Rate limit de 10 intentos por minuto e IP. No revelar si el correo existe. No registrar contraseña ni tokens.

## 4. Contrato

### Request

```json
{
  "email": "david@example.com",
  "password": "frase larga de ejemplo",
  "device_id": "android-01"
}
```

### Response

`200 OK` con el mismo objeto de sesión del registro: `user`, `access_token`, `refresh_token`, `token_type`, `expires_in`.

### Errores

| Código HTTP | `code` | Causa |
|---|---|---|
| 401 | `INVALID_CREDENTIALS` | Correo o contraseña incorrectos |
| 401 | `UNAUTHORIZED` | Cuenta no activa |
| 429 | `RATE_LIMITED` | Demasiados intentos o rate limit |
| 422 | `VALIDATION_ERROR` | Formato inválido |
| 500 | `INTERNAL_ERROR` | Fallo inesperado seguro |

## 5. Arquitectura y decisiones

| Ruta | Responsabilidad |
|---|---|
| `Pactogg.Api/Modules/Auth/AuthEndpoints.cs` | Ruta `POST /login` |
| `Pactogg.Api/Modules/Auth/AuthService.cs` | `LoginAsync` |
| `Pactogg.Api/Infrastructure/Redis/RedisLoginAttemptStore.cs` | Fallos |
| `Pactogg.Api/Infrastructure/Security/` | Hash y tokens |

- **AUTH01-D1:** bloqueo por correo + IP, no solo por correo.
- **AUTH01-D2:** respuesta genérica para credenciales inválidas.
- **AUTH01-D3:** con correo inexistente o cuenta solo de Google se verifica un hash Argon2id ficticio para igualar el trabajo (Q-020 resuelta, F2-D51). Prueba `SddDecisionTests.Q020_LoginWithUnknownEmail_DoesTheSameHashWorkAsAWrongPassword`.

## 6. Criterios de aceptación

| ID | Escenario | Evidencia |
|---|---|---|
| AUTH01-AC1 | Credenciales válidas devuelven 200 y sesión | Flujo de integración |
| AUTH01-AC2 | Credenciales inválidas devuelven 401 genérico | Revisión de servicio; falta prueba dedicada |
| AUTH01-AC3 | 5 fallos bloquean 15 minutos | Código; falta prueba dedicada |
| AUTH01-AC4 | Token inválido/ausente devuelve 401 | SecurityTest |
| AUTH01-AC5 | Contraseña no se registra | Revisión de logs |

## 7. Plan de pruebas

Flujo de registro/login en IntegrationTest y 401 de endpoints protegidos en SecurityTest. Faltan pruebas dedicadas de bloqueo y de tiempo de respuesta.

## 8. Riesgos y deuda

Enumeración por tiempo de hash. No hay prueba de bloqueo. Redis ausente usa fallback local. Sin refresh test dedicado.

## Aprobación

| Estado | Revisores | Fecha | Detalle |
|---|---|---|---|
| Implementación local | Agente | 2026-11-03 | Build y pruebas |
| Aceptación externa | Pendiente | Pendiente | AWS/TLS/revisión |

[Plan](plan.md), [tareas](tasks.md), [checklist](checklists/checklist.md).

