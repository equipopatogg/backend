# Tareas — VOT-04 — POST /api/v1/contracts/{id}/votes/resolve

[Spec](spec.md) · [plan](plan.md).

- [x] VOT-04-T1 — RF1: Solo rol admin del grupo (403 NOT_GROUP_ADMIN).
- [x] VOT-04-T2 — RF2: Solo en voting y si: no hay jurados, todos votaron sin consenso o pasaron las 48 h; si no → 409 VOTING_NOT_FINISHED.
- [x] VOT-04-T3 — RF3: outcome fulfilled|cheating; misma transacción de cierre que VOT-02 con resolved_by=admin.
- [x] T4 — Pruebas: `JuryFlowTests.cs`, `AuthorizationTests.cs`, `JuryFlowTests.TiedVote_IsResolvedOnlyByNeutralAdmin`.
- [x] T5 — `api/openapi.yaml` y `docs/API-movil.md` actualizados.

## Definición de terminado

Pruebas en verde, OpenAPI actualizado y checklist completo.
