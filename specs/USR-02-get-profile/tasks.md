# Tareas — USR-02 — GET /api/v1/users/me

[Spec](spec.md) · [plan](plan.md).

- [x] USR-02-T1 — RF1: exigir token válido.
- [x] USR-02-T2 — RF2: devolver id, email, nickname, avatar_id, email_verified, stats (completed, cheated, streak, trophies, word_index) y groups_count.
- [x] USR-02-T3 — RF3: caché Redis 60 s por usuario con llave versionada; se invalida al editar el perfil, crear o unirse a un grupo y al aceptar un reto.
- [x] T4 — Pruebas: `SocialFlowTests.Profile_GetUpdateAndCheckNickname`, `SocialFlowTests`, `AuthorizationTests`, `SocialFlowTests.cs`, `AuthorizationTests.cs`.
- [x] T5 — `api/openapi.yaml` y `docs/API-movil.md` actualizados.

## Definición de terminado

Pruebas en verde, OpenAPI actualizado y checklist completo.
