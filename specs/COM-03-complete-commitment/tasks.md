# Tareas — COM-03 — POST /api/v1/commitments/{id}/complete

[Spec](spec.md) · [plan](plan.md).

- [x] COM-03-T1 — RF1: Solo acreedor (403 NOT_COMMITMENT_PARTY).
- [x] COM-03-T2 — RF2: pending o completion_requested → completed; si ya estaba → 409 INVALID_COMMITMENT_STATE.
- [x] COM-03-T3 — RF3: Avisa al deudor.
- [x] T4 — Pruebas: `JuryFlowTests.cs`, `AuthorizationTests.cs`.
- [x] T5 — `api/openapi.yaml` y `docs/API-movil.md` actualizados.

## Definición de terminado

Pruebas en verde, OpenAPI actualizado y checklist completo.
