# AUTH-06 / AUTH-07 — Recuperar contraseña y cambio de contraseña

**Fecha:** 2026-11-05 · **Estado:** Implementado y verificado localmente · **Historia:** ID 16 Recuperar contraseña por correo · **Feature:** [002](../002-cuentas-y-sesion/spec.md)

## 1. Endpoints

- `POST /api/v1/auth/forgot-password` (público) `{ "email" }` → siempre `202`.
- `POST /api/v1/auth/reset-password` (público) `{ "token", "new_password" }` → `200`.
- `POST /api/v1/users/me/password` (usuario) `{ "current_password", "new_password", "device_id?" }` → `200` sesión nueva.

## 2. Requisitos funcionales

- RF1: la respuesta de AUTH-06 es la misma exista o no la cuenta (CLARIFICACION #11); máximo 3 correos por hora por cuenta.
- RF2: enlace `pactogg://auth/reset-password?token=...`, de un solo uso, caduca en 1 hora; se guarda solo el hash.
- RF3: restablecer revoca todas las sesiones y marca el correo como verificado.
- RF4: contraseña nueva de 12 a 128 caracteres, sin reglas de composición (CLARIFICACION seguridad).
- RF5: cambio de contraseña: exige la actual (`422 INVALID_CREDENTIALS`), distinta de la nueva, máximo 5 intentos en 15 min; revoca todas las sesiones y devuelve una nueva. Cuentas solo-Google → `409 PASSWORD_NOT_SET` (usan AUTH-06 para crear una).
- RF6: los refresh revocados por cambio de contraseña o cierre de sesión dan 401 sin disparar la detección de reutilización; solo un refresh **rotado** que reaparece revoca todo (CLARIFICACION #10).

## 3. Criterios de aceptación

`AccountFlowTests.ForgotAndReset_AreGeneric_SingleUse_AndRevokeSessions`, `AccountFlowTests.ChangePassword_RequiresCurrent_AndReturnsFreshSession`.
