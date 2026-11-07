# Tareas — AUTH-05 — Verificación de correo

[Spec](spec.md) · [plan](plan.md).

- [x] AUTH-05-T1 — RF1: al registrarse con correo se envía el correo de verificación (best-effort; el registro no falla si el correo no sale).
- [x] AUTH-05-T2 — RF2: el token es aleatorio de 256 bits, de un solo uso y caduca en 24 h; en Redis solo se guarda su SHA-256 (CLARIFICACION seguridad).
- [x] AUTH-05-T3 — RF3: el correo trae un deep link `pactogg://auth/verify-email?token=...` y el mismo token como código para pegar.
- [x] AUTH-05-T4 — RF4: reenviar: máximo 3 por hora por usuario; si ya está verificado → `409 EMAIL_ALREADY_VERIFIED`.
- [x] AUTH-05-T5 — RF5: con `Auth:RequireVerifiedEmail=true`, crear o unirse a grupos exige correo verificado (`403 EMAIL_NOT_VERIFIED`, CLARIFICACION #5) y los jurados se sortean solo
- [x] AUTH-05-T6 — RF6: límite por IP de 10 solicitudes por hora en las rutas de cuenta.
- [x] T7 — Pruebas: `AccountAndPushTests.cs`, `AccountFlowTests.Register_SendsVerification_AndGroupsRequireVerifiedEmail`.
- [x] T8 — `api/openapi.yaml` y `docs/API-movil.md` actualizados.

## Definición de terminado

Pruebas en verde, OpenAPI actualizado y checklist completo.
