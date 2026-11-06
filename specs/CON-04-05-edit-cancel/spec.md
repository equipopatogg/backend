# CON-04 / CON-05 — Editar y cancelar un reto

**Fecha:** 2026-11-05 · **Estado:** Implementado y verificado localmente · **Historia:** ID 43 Publicar reto

## 1. Endpoints

- `PATCH /api/v1/contracts/{id}` `{ "version", "game?", "condition?", "reward?", "deadline?", "bond_required?" }` → `200`.
- `DELETE /api/v1/contracts/{id}?version=` → `204` (queda `cancelled`, `closed_reason=cancelled_by_challenger`).

## 2. Requisitos funcionales

- RF1: solo el retador (`403 NOT_CONTRACT_OWNER`), solo en `open` y sin aceptación (409).
- RF2: bloqueo optimista: `version` obligatoria en PATCH; si otro lo cambió → 409. La versión sube en 1.
- RF3: mismas validaciones que CON-01: deadline futura, fianza permitida por el grupo y moderación de condición y recompensa.
- RF4: auditoría `contract.updated` / `contract.cancelled`; se invalida el tablero.

## 3. Criterios de aceptación

`ProfileContractAuditPushTests.Challenger_EditsAndCancelsOpenContract_AdminSeesAudit`.
