# Tareas — COM-01 — GET /api/v1/groups/{gid}/commitments

[Spec](spec.md) · [plan](plan.md).

- [x] COM-01-T1 — RF1: role=debo (soy deudor, por defecto) o me-deben (soy acreedor); otro → 422.
- [x] COM-01-T2 — RF2: status pending|completion_requested|completed; cursor.
- [x] COM-01-T3 — RF3: Cada compromiso con deudor y acreedor (apodo), descripción (la recompensa) y juego. Se crean solos al cumplirse un reto (uno por reto, índice único).
- [x] T4 — Pruebas: `JuryFlowTests.cs`, `AuthorizationTests.cs`.
- [x] T5 — `api/openapi.yaml` y `docs/API-movil.md` actualizados.

## Definición de terminado

Pruebas en verde, OpenAPI actualizado y checklist completo.
