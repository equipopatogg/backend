# AUTH-05 — Verificación de correo

**Fecha:** 2026-11-05 · **Estado:** Implementado y verificado localmente · **Historia:** ID 17 Correo de verificación · **Feature:** [002](../002-cuentas-y-sesion/spec.md)

## 1. Endpoints

- `POST /api/v1/auth/verify-email` (público) `{ "token": "..." }` → `200 { email_verified: true }`.
- `POST /api/v1/auth/verify-email/resend` (usuario) → `202`.

## 2. Requisitos funcionales

- RF1: al registrarse con correo se envía el correo de verificación (best-effort; el registro no falla si el correo no sale).
- RF2: el token es aleatorio de 256 bits, de un solo uso y caduca en 24 h; en Redis solo se guarda su SHA-256 (CLARIFICACION seguridad).
- RF3: el correo trae un deep link `pactogg://auth/verify-email?token=...` y el mismo token como código para pegar.
- RF4: reenviar: máximo 3 por hora por usuario; si ya está verificado → `409 EMAIL_ALREADY_VERIFIED`.
- RF5: con `Auth:RequireVerifiedEmail=true`, crear o unirse a grupos exige correo verificado (`403 EMAIL_NOT_VERIFIED`, CLARIFICACION #5) y los jurados se sortean solo entre miembros verificados (CLARIFICACION #223). Google ya llega verificado.
- RF6: límite por IP de 10 solicitudes por hora en las rutas de cuenta.

## 3. Errores

| HTTP | `code` | Causa |
|---|---|---|
| 400 | `INVALID_OR_EXPIRED_TOKEN` | Token inválido, caducado o ya usado |
| 403 | `EMAIL_NOT_VERIFIED` | Crear/unirse a grupo sin verificar |
| 409 | `EMAIL_ALREADY_VERIFIED` | Reenviar con correo ya verificado |
| 429 | `RATE_LIMITED` | Límite |

## 4. Configuración

`Email:SmtpHost/SmtpPort/SmtpUser/SmtpPassword/FromAddress` (SES por SMTP: `email-smtp.us-east-1.amazonaws.com:587`; en modo pruebas hay que verificar remitente y destinatarios). Sin `SmtpHost` no se envía nada. `Auth:RequireVerifiedEmail` queda en `false` hasta configurar el correo en producción.

## 5. Criterios de aceptación

`IntegrationTest/AccountAndPushTests.cs` → `AccountFlowTests.Register_SendsVerification_AndGroupsRequireVerifiedEmail`.

## Enlace web de verificación (2026-11-16, F2-D75)

- El enlace del correo se configura con `Email:VerifyEmailLink`. Por defecto sigue siendo `pactogg://auth/verify-email`; en producción puede apuntar a la página web publicada (`https://…/verificar-correo`), que recibe `?token=` y llama a `POST /api/v1/auth/verify-email` sin sesión.
- La API solo acepta esa llamada desde el navegador si el origen exacto de la página está en `Cors:AllowedOrigins` (sin comodines). El endpoint, el cuerpo, la caducidad de 24 h y el uso único del token no cambian.
- Se configura con `scripts/configure-web.sh` y aplica al recrear las instancias.
- Pendiente de verificar en producción: correo nuevo con enlace web, verificación correcta y rechazo del token ya usado (`422 INVALID_OR_EXPIRED_TOKEN`).
