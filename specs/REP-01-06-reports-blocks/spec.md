# REP-01..06 — Reportes, eliminación de contenido y bloqueos

**Fecha:** 2026-11-06 · **Estado:** Aprobada e implementada (v0.9.0) · **Feature:** spec 007 Evidencias y moderación (adelantada)
**Fuentes:** CLARIFICACION #19, #20, #130; Descripción del Backend 7.11; constitución §7.1 (rate limit en reportes); CRUD hoja Reportes y auditoría.

## Endpoints

| ID | Método y ruta | Quién | Respuesta |
|---|---|---|---|
| REP-01 | `POST /api/v1/reports` `{ group_id, target_type: evidence|comment|user, target_id, reason, details? }` | miembro | `201` |
| REP-02 | `GET /api/v1/groups/{gid}/reports?status=` | admin | `200` lista con cursor |
| REP-03 | `POST /api/v1/reports/{id}/resolve` `{ action: dismiss|remove_content }` | admin | `200` |
| REP-04 | `DELETE /api/v1/groups/{gid}/content/{type}/{id}` (`evidence` o `comment`) | admin | `204` |
| REP-05 | `POST /api/v1/users/{id}/block` | usuario | `204` (idempotente) |
| REP-06 | `DELETE /api/v1/users/{id}/block` | usuario | `204` (idempotente) |

## Requisitos (EARS)

- **REP-R1:** Cuando un miembro reporte, el objetivo deberá pertenecer al grupo indicado; si no, `404` (anti-IDOR). Motivos: ofensivo, sexual, odio, spam, trampa, otro.
- **REP-R2:** Si la misma persona reporta el mismo objetivo otra vez, entonces `409 ALREADY_REPORTED`.
- **REP-R3:** Mientras alguien envíe más de 10 reportes por hora, el sistema deberá responder `429`.
- **REP-R4:** Cuando el objetivo sea una cuenta, el reporte deberá quedar `escalated`: lo revisa el administrador de la plataforma, no el del grupo (CLARIFICACION #19).
- **REP-R5:** Cuando un admin resuelva con `remove_content` o use REP-04, el sistema deberá ocultar la evidencia (`removed`, se borra de S3 y sale del Muro de la Fama) o borrar el comentario del voto, cerrar los demás reportes del mismo objetivo y auditar.
- **REP-R6:** Si quien consulta o resuelve no es admin, `403 NOT_GROUP_ADMIN`; un reporte ya resuelto o escalado no se resuelve otra vez (`409`).
- **REP-R7:** Mientras A tenga bloqueada a B, B no podrá retar directamente a A (`403 BLOCKED_BY_USER`) (REP-D2).

## Decisiones (aprobadas por el PO el 2026-11-06)

- **REP-D1:** sin pantalla de administrador de la plataforma en esta versión; los reportes de cuentas quedan `escalated` y la apelación es por correo (CLARIFICACION #20).
- **REP-D2:** el bloqueo impide los retos directos; los retos para todo el grupo no se filtran.

## Criterios de aceptación

| ID | Requisito | Prueba |
|---|---|---|
| REP-AC1 | R1, R2, R5, R6 | `ReportsAndBlocksTests.Report_ListAndResolveByRemovingContent` |
| REP-AC2 | R1, R4 | `ReportsAndBlocksTests.Report_TargetFromAnotherGroup_Is404_AndUserReportsAreEscalated` |
| REP-AC3 | R5, R6 | `ReportsAndBlocksTests.AdminRemovesContentDirectly_MemberCannot` |
| REP-AC4 | R7 | `ReportsAndBlocksTests.Block_StopsDirectedChallenges_UntilUnblocked` |
| REP-AC5 | R3 | Política `report` en `RedisRateLimitMiddleware` (sin prueba dedicada) |

Expediente: [plan](plan.md), [tareas](tasks.md), [checklist](checklists/checklist.md).
