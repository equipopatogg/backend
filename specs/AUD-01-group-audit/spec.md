# AUD-01 — Historial inmutable del grupo

**Fecha:** 2026-11-05 · **Estado:** Implementado y verificado localmente · **Historia:** ID 160 Historial inmutable de contratos

## 1. Endpoint

`GET /api/v1/groups/{gid}/audit?entity=&entity_id=&limit=&cursor=` → eventos del más reciente al más antiguo.

## 2. Requisitos funcionales

- RF1: solo administradores del grupo (`403 NOT_GROUP_ADMIN`).
- RF2: cada evento trae acción, actor (apodo), entidad, datos y el hash encadenado con el anterior (SHA-256), así que una alteración se detecta.
- RF3: AUD-02: no existen rutas para modificar o borrar auditoría.
- RF4: índice `ix_audit_group_id` (group_id, _id desc).

## 3. Criterios de aceptación

`ProfileContractAuditPushTests.Challenger_EditsAndCancelsOpenContract_AdminSeesAudit`.
