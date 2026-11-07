# Tareas — USR-03 — PATCH /api/v1/users/me

[Spec](spec.md) · [plan](plan.md).

- [x] USR-03-T1 — RF1: cuerpo con nickname y/o avatar_id; al menos uno (si no, 422).
- [x] USR-03-T2 — RF2: nickname `^[A-Za-z0-9_.-]{3,24}$` (mismo patrón del registro, D-15).
- [x] USR-03-T3 — RF3: apodo único sin distinguir mayúsculas; conflicto → 409 NICKNAME_ALREADY_EXISTS (también si el índice único gana la carrera).
- [x] USR-03-T4 — RF4: el apodo pasa por moderación de texto; rechazo → 422 CONTENT_REJECTED; moderación caída → 503 (falla cerrada).
- [x] USR-03-T5 — RF5: avatar_id `^[a-z0-9_-]{1,32}$` (avatares predefinidos de la app).
- [x] USR-03-T6 — RF6: actualizar nick_lower en group_members para el autocompletado y auditar `user.profile_updated`.
- [x] USR-03-T7 — RF7: campos desconocidos (p. ej. email) → 400 INVALID_INPUT.
- [x] T8 — Pruebas: `SocialFlowTests`, `SocialFlowTests.cs`, `AuthorizationTests.cs`.
- [x] T9 — `api/openapi.yaml` y `docs/API-movil.md` actualizados.

## Definición de terminado

Pruebas en verde, OpenAPI actualizado y checklist completo.
