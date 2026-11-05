# AUTH-02 — POST /api/v1/auth/google

**Fecha:** 2026-11-04 · **Estado:** Implementado y verificado localmente · **Historia:** ID 12 Registro mediante Google · **Feature:** [002](../002-cuentas-y-sesion/spec.md)

## 1. Objetivo

Entrar o crear la cuenta con Google desde la app, sin contraseña.

## 2. Requisitos funcionales

- RF1: público, con límite de peticiones de login (10/min por IP).
- RF2: `id_token` validado con las llaves públicas de Google (descubrimiento OpenID en caché): firma, emisor `accounts.google.com`, audiencia en `Auth:GoogleClientIds`, vigencia (±2 min) y `email_verified=true`.
- RF3: si existe cuenta con ese `google_sub` → 200; si existe con el mismo correo → se vincula (`google_sub`, `email_verified=true`) y 200.
- RF4: cuenta nueva: exige `nickname` (si falta, `422 NICKNAME_REQUIRED` con `details.email` y `details.suggested_name`), `accept_privacy=true` y `privacy_version` vigente; crea la cuenta sin contraseña → 201.
- RF5: sin Client IDs configurados → `503 GOOGLE_AUTH_UNAVAILABLE` (falla cerrada); Google caído → 503.
- RF6: auditoría `auth.login_google`, `auth.google_linked`, `user.registered_google`.

## 3. Errores

| HTTP | `code` | Causa |
|---|---|---|
| 401 | `INVALID_GOOGLE_TOKEN` | Token inválido, vencido, de otra app o correo sin verificar |
| 409 | `NICKNAME_ALREADY_EXISTS` | Apodo ocupado |
| 422 | `NICKNAME_REQUIRED` / `VALIDATION_ERROR` | Falta apodo o privacidad |
| 429 | `RATE_LIMITED` | Límite |
| 503 | `GOOGLE_AUTH_UNAVAILABLE` | Sin configurar o Google no responde |

## 4. Implementación

`Pactogg.Api/Modules/Auth/AuthService.cs` (GoogleAsync), `Pactogg.Api/Infrastructure/Security/GoogleTokenValidator.cs`, índice `ux_users_google_sub` (único parcial).

## 5. Configuración

`appsettings.json` → `Auth:GoogleClientIds: ["<client-id-web>.apps.googleusercontent.com"]`. El Client ID no es secreto. La app Android debe pedir el ID token con ese mismo Client ID web (`setServerClientId`).

## 6. Criterios de aceptación

| Escenario | Evidencia |
|---|---|
| Sin apodo → 422; con apodo → 201; de nuevo → 200; login con contraseña → 401 | `IntegrationTest/GoogleAuthTests.cs` |
| Vincula correo existente; token falso o sin verificar → 401 | `IntegrationTest/GoogleAuthTests.cs` |
| Sin Client ID → 503 | `UnitTest/GoogleTokenValidatorTests.cs` |
