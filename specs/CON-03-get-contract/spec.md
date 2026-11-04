# CON-03 — GET /api/v1/contracts/{id}

**Fecha:** 2026-11-04
**Estado:** Implementado y verificado localmente (pruebas de integración en verde); despliegue con `scripts/release.sh`.
**Contrato:** `api/openapi.yaml` (v0.5.0)

## 1. Objetivo

Detalle de un reto.

## 2. Requisitos funcionales

- RF1: id inválido o inexistente → 404.
- RF2: solo miembros del grupo del reto (403).
- RF3: incluye accepted_by, accepted_at, closed_reason, evidence_count, version y updated_at.
- RF4: sin caché (lectura por _id); se prioriza ver el estado exacto antes de aceptar.

## 3. Errores

| HTTP | `code` | Causa |
|---|---|---|
| 401 | `UNAUTHORIZED` | Sin token |
| 403 | `NOT_GROUP_MEMBER` | No pertenece |
| 404 | `NOT_FOUND` | No existe |

## 4. Implementación

- `Pactogg.Api/Modules/Contracts/ContractService.cs (GetAsync)`

## 5. Criterios de aceptación

| ID | Escenario | Evidencia |
|---|---|---|
| CON03-AC1 | Miembro → 200 open | SocialFlowTests |
| CON03-AC2 | Ajeno → 403; inexistente → 404 | SocialFlowTests |

Pruebas: `IntegrationTest/SocialFlowTests.cs` (fakes en memoria) y `SecurityTest/AuthorizationTests.cs` (401).
