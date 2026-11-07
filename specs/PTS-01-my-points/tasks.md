# Tareas — PTS-01 — GET /api/v1/groups/{gid}/points/me

[Spec](spec.md) · [plan](plan.md).

- [x] PTS-01-T1 — RF1: Miembros (403).
- [x] PTS-01-T2 — RF2: balance actual + movimientos del libro (points_ledger) del más reciente al más antiguo; filtro type; cursor.
- [x] PTS-01-T3 — RF3: No existe ningún endpoint de compra; los movimientos solo los crea el backend en transacciones.
- [x] T4 — Pruebas: `JuryFlowTests.cs`, `AuthorizationTests.cs`.
- [x] T5 — `api/openapi.yaml` y `docs/API-movil.md` actualizados.

## Definición de terminado

Pruebas en verde, OpenAPI actualizado y checklist completo.
