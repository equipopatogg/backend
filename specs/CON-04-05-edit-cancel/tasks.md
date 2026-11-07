# Tareas — CON-04 / CON-05 — Editar y cancelar un reto

[Spec](spec.md) · [plan](plan.md).

- [x] CON-04-T1 — RF1: solo el retador (`403 NOT_CONTRACT_OWNER`), solo en `open` y sin aceptación (409).
- [x] CON-04-T2 — RF2: bloqueo optimista: `version` obligatoria en PATCH; si otro lo cambió → 409. La versión sube en 1.
- [x] CON-04-T3 — RF3: mismas validaciones que CON-01: deadline futura, fianza permitida por el grupo y moderación de condición y recompensa.
- [x] CON-04-T4 — RF4: auditoría `contract.updated` / `contract.cancelled`; se invalida el tablero.
- [x] T5 — Pruebas: `ProfileContractAuditPushTests.Challenger_EditsAndCancelsOpenContract_AdminSeesAudit`.
- [x] T6 — `api/openapi.yaml` y `docs/API-movil.md` actualizados.

## Definición de terminado

Pruebas en verde, OpenAPI actualizado y checklist completo.
