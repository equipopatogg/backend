# Tareas — REP-01..06 — Reportes, eliminación de contenido y bloqueos

[Spec](spec.md) · [plan](plan.md).

- [x] REP-01-T1 — REP-R1: Cuando un miembro reporte, el objetivo deberá pertenecer al grupo indicado; si no, `404` (anti-IDOR). Motivos: ofensivo, sexual, odio, spam, trampa, otro.
- [x] REP-01-T2 — REP-R2: Si la misma persona reporta el mismo objetivo otra vez, entonces `409 ALREADY_REPORTED`.
- [x] REP-01-T3 — REP-R3: Mientras alguien envíe más de 10 reportes por hora, el sistema deberá responder `429`.
- [x] REP-01-T4 — REP-R4: Cuando el objetivo sea una cuenta, el reporte deberá quedar `escalated`: lo revisa el administrador de la plataforma, no el del grupo (CLARIFICACION #19).
- [x] REP-01-T5 — REP-R5: Cuando un admin resuelva con `remove_content` o use REP-04, el sistema deberá ocultar la evidencia (`removed`, se borra de S3 y sale del Muro de la Fama) o borr
- [x] REP-01-T6 — REP-R6: Si quien consulta o resuelve no es admin, `403 NOT_GROUP_ADMIN`; un reporte ya resuelto o escalado no se resuelve otra vez (`409`).
- [x] REP-01-T7 — REP-R7: Mientras A tenga bloqueada a B, B no podrá retar directamente a A (`403 BLOCKED_BY_USER`) (REP-D2).
- [x] T8 — Pruebas: `ReportsAndBlocksTests.Report_ListAndResolveByRemovingContent`, `ReportsAndBlocksTests.Report_TargetFromAnotherGroup_Is404_AndUserReportsAreEscalated`, `ReportsAndBlocksTests.AdminRemovesContentDirectly_MemberCannot`, `ReportsAndBlocksTests.Block_StopsDirectedChallenges_UntilUnblocked`.
- [x] T9 — `api/openapi.yaml` y `docs/API-movil.md` actualizados.

## Definición de terminado

Pruebas en verde, OpenAPI actualizado y checklist completo.
