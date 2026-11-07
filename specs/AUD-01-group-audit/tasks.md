# Tareas — AUD-01 — Historial inmutable del grupo

[Spec](spec.md) · [plan](plan.md).

- [x] AUD-01-T1 — RF1: solo administradores del grupo (`403 NOT_GROUP_ADMIN`).
- [x] AUD-01-T2 — RF2: cada evento trae acción, actor (apodo), entidad, datos y el hash encadenado con el anterior (SHA-256), así que una alteración se detecta.
- [x] AUD-01-T3 — RF3: AUD-02: no existen rutas para modificar o borrar auditoría.
- [x] AUD-01-T4 — RF4: índice `ix_audit_group_id` (group_id, _id desc).
- [x] T5 — Pruebas: `ProfileContractAuditPushTests.Challenger_EditsAndCancelsOpenContract_AdminSeesAudit`.
- [x] T6 — `api/openapi.yaml` y `docs/API-movil.md` actualizados.

## Definición de terminado

Pruebas en verde, OpenAPI actualizado y checklist completo.
