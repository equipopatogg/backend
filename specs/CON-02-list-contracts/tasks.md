# Tareas — CON-02 — GET /api/v1/groups/{gid}/contracts

[Spec](spec.md) · [plan](plan.md).

- [x] CON-02-T1 — RF1: solo miembros (403); grupo inexistente → 404.
- [x] CON-02-T2 — RF2: status ∈ open, in_progress, voting, fulfilled, cheating, expired, cancelled (otro → 422).
- [x] CON-02-T3 — RF3: mine=true → retador, retado o quien aceptó.
- [x] CON-02-T4 — RF4: game exacto sin distinguir mayúsculas; q en condición, recompensa o juego (máx. 60, escapado).
- [x] CON-02-T5 — RF5: del más reciente al más antiguo con cursor; limit 1–50.
- [x] CON-02-T6 — RF6: caché 30 s por versión del grupo; se invalida al crear, aceptar, rechazar o subir evidencia.
- [x] T7 — Pruebas: `SocialFlowTests.Contracts_BoardDetailAcceptAndReject`, `SocialFlowTests`, `SocialFlowTests.cs`, `AuthorizationTests.cs`.
- [x] T8 — `api/openapi.yaml` y `docs/API-movil.md` actualizados.

## Definición de terminado

Pruebas en verde, OpenAPI actualizado y checklist completo.
