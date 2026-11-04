# USR-01 — POST /api/v1/auth/register

**Fecha:** 2026-11-03
**Estado:** Implementado y verificado localmente; aceptación externa pendiente.
**IDs:** USR-01, AUTH-05, BE-11, BE-13
**Contrato:** `api/openapi.yaml`

## 1. Contexto y objetivo

Crear una cuenta activa con apodo obligatorio, consentimiento de privacidad y sesión JWT inicial. Es el primer endpoint del flujo de evaluación.

## 2. Requisitos funcionales

- RF1: aceptar `email`, `password`, `nickname`, `accept_privacy`, `privacy_version` y `device_id` opcional.
- RF2: rechazar `accept_privacy=false` o una versión de privacidad distinta de la vigente.
- RF3: normalizar correo a minúsculas y apodo con `trim`; correo y apodo son únicos.
- RF4: almacenar la contraseña con Argon2id; nunca devolverla ni registrarla.
- RF5: crear usuario `active`, `email_verified=false`, consentimiento y auditoría `user.registered`.
- RF6: emitir access token JWT de 15 minutos y refresh token de 30 días; guardar solo el hash SHA-256 del refresh.
- RF7: responder `201` con la sesión y el usuario.

## 3. Requisitos no funcionales

Rate limit de 5 registros por hora e IP. Sin PII en logs. Rechazo de campos desconocidos y claves NoSQL. El correo no se registra completo en logs.

## 4. Contrato

### Request

```json
{
  "email": "david@example.com",
  "password": "frase larga de ejemplo",
  "nickname": "DavidGG",
  "accept_privacy": true,
  "privacy_version": "2026-10-01",
  "device_id": "android-01"
}
```

Validaciones: `email` válido y máximo 254; `password` 12–128; `nickname` regex `^[A-Za-z0-9_.-]{3,24}$`; `privacy_version` 1–32.

### Response

`201 Created`:

```json
{
  "data": {
    "user": {
      "id": "…",
      "email": "david@example.com",
      "nickname": "DavidGG",
      "email_verified": false,
      "created_at": "…"
    },
    "access_token": "…",
    "refresh_token": "…",
    "token_type": "Bearer",
    "expires_in": 900
  }
}
```

### Errores

| Código HTTP | `code` | Causa |
|---|---|---|
| 409 | `EMAIL_ALREADY_EXISTS` | Correo ya registrado |
| 409 | `NICKNAME_ALREADY_EXISTS` | Apodo ya usado |
| 422 | `VALIDATION_ERROR` | Formato, consentimiento o versión inválidos |
| 400 | `INVALID_INPUT` | JSON o campo desconocido inválido |
| 429 | `RATE_LIMITED` | Límite de registro |
| 500 | `INTERNAL_ERROR` | Fallo inesperado seguro |

## 5. Arquitectura y decisiones

| Ruta | Responsabilidad |
|---|---|
| `Pactogg.Api/Modules/Auth/AuthEndpoints.cs` | Ruta y validación |
| `Pactogg.Api/Modules/Auth/AuthService.cs` | Registro, unicidad, sesión y auditoría |
| `Pactogg.Api/Modules/Auth/Dtos/AuthDtos.cs` | DTOs |
| `Pactogg.Api/Infrastructure/Security/Argon2PasswordHasher.cs` | Hash |
| `Pactogg.Api/Infrastructure/Security/JwtTokenService.cs` | Tokens |

- **USR01-D1:** apodo obligatorio (D-15).
- **USR01-D2:** contraseña 12–128 sin composición (D-16).
- **USR01-D3:** consentimiento con versión vigente (D-21).
- **USR01-D4:** rango implementado de apodo 3–24; el texto histórico 3–30 queda pendiente de reconciliación (Q-019).

## 6. Criterios de aceptación

| ID | Escenario | Evidencia |
|---|---|---|
| USR01-AC1 | Registro válido devuelve 201 y sesión | `RegisterGroupContractEvidenceTournamentsAndLogout_WorksEndToEnd` |
| USR01-AC2 | Correo/apodo duplicado devuelve 409 | Código y pruebas de servicio; falta prueba dedicada |
| USR01-AC3 | Consentimiento/versión inválidos devuelven 422 | Validación de servicio |
| USR01-AC4 | Contraseña no se devuelve ni registra | Revisión de DTO/logs y hasher |
| USR01-AC5 | Campo desconocido se rechaza | `Request_WithUnknownField_Returns400` |

## 7. Plan de pruebas

Flujo de registro en IntegrationTest, hasher en UnitTest, campo desconocido y NoSQL en Security/Integration. Falta prueba dedicada de duplicados y de rate limit de registro.

## 8. Riesgos y deuda

No hay verificación de correo ni recuperación de contraseña. La prueba de duplicados no está dedicada. La conexión Atlas se verificó localmente; falta E2E del registro. El apodo máximo difiere del texto histórico (Q-019).

## Aprobación

| Estado | Revisores | Fecha | Detalle |
|---|---|---|---|
| Implementación local | Agente | 2026-11-03 | Build y pruebas |
| Aceptación externa | Pendiente | Pendiente | AWS/TLS/revisión |

[Plan](plan.md), [tareas](tasks.md), [checklist](checklists/checklist.md).

