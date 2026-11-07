# PTS-03, COM-02, COM-05 y ACH-03 — Detalles y resúmenes

**Fecha:** 2026-11-06 · **Estado:** Aprobada e implementada (v0.9.0) · **Features:** 003 (puntos), 009 (compromisos), 008 (ranking)
**Fuentes:** CRUD hojas Puntos, Compromisos y Logros y ranking; CLARIFICACION #112, #113, #115.

## Endpoints y requisitos (EARS)

| ID | Método y ruta | Requisito |
|---|---|---|
| PTS-03 | `GET /api/v1/points/movements/{id}` | Cuando el dueño del movimiento o un admin de su grupo lo pida, el sistema deberá devolverlo; a cualquier otro miembro, `404`; a un ajeno al grupo, `403`. |
| COM-02 | `GET /api/v1/commitments/{id}` | Cuando un miembro del grupo pida un compromiso, el sistema deberá devolverlo con deudor, acreedor, descripción y estado. |
| COM-05 | `GET /api/v1/groups/{gid}/commitments/summary` | Cuando un miembro lo pida, el sistema deberá devolver `{ debo, me_deben }` con los compromisos `pending` o `completion_requested`. |
| ACH-03 | `GET /api/v1/groups/{gid}/ranking` | Cuando un miembro lo pida, el sistema deberá devolver el ranking por puntos (sin temporadas en la primera versión) y `king`, el primer lugar si no hay empate. |

## Criterios de aceptación

| ID | Prueba |
|---|---|
| DET-AC1 (PTS-03, ACH-03) | `DetailEndpointsTests.PointsMovement_OnlyOwnerOrAdmin_AndRankingHasKing` |
| DET-AC2 (COM-02, COM-05) | `DetailEndpointsTests.CommitmentDetailAndSummary` |

Expediente: [plan](plan.md), [tareas](tasks.md), [checklist](checklists/checklist.md).
