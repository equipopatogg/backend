# Tareas — AUTH-06 / AUTH-07 — Recuperar contraseña y cambio de contraseña

[Spec](spec.md) · [plan](plan.md).

- [x] AUTH-06-T1 — RF1: la respuesta de AUTH-06 es la misma exista o no la cuenta (CLARIFICACION #11); máximo 3 correos por hora por cuenta.
- [x] AUTH-06-T2 — RF2: enlace `pactogg://auth/reset-password?token=...`, de un solo uso, caduca en 1 hora; se guarda solo el hash.
- [x] AUTH-06-T3 — RF3: restablecer revoca todas las sesiones y marca el correo como verificado.
- [x] AUTH-06-T4 — RF4: contraseña nueva de 12 a 128 caracteres, sin reglas de composición (CLARIFICACION seguridad).
- [x] AUTH-06-T5 — RF5: cambio de contraseña: exige la actual (`422 INVALID_CREDENTIALS`), distinta de la nueva, máximo 5 intentos en 15 min; revoca todas las sesiones y devuelve una n
- [x] AUTH-06-T6 — RF6: los refresh revocados por cambio de contraseña o cierre de sesión dan 401 sin disparar la detección de reutilización; solo un refresh **rotado** que reaparece r
- [x] T7 — Pruebas: `AccountFlowTests.ForgotAndReset_AreGeneric_SingleUse_AndRevokeSessions`, `AccountFlowTests.ChangePassword_RequiresCurrent_AndReturnsFreshSession`.
- [x] T8 — `api/openapi.yaml` y `docs/API-movil.md` actualizados.

## Definición de terminado

Pruebas en verde, OpenAPI actualizado y checklist completo.
