# CON-07 — POST /api/v1/contracts/{id}/reject

**Fecha:** 2026-11-04
**Estado:** Implementado y verificado localmente (pruebas de integración en verde); despliegue con `scripts/release.sh`.
**Contrato:** `api/openapi.yaml` (v0.5.0)

## 1. Objetivo

El retado rechaza un reto dirigido a él.

## 2. Requisitos funcionales

- RF1: solo target_type=member y target_id = usuario (otro → 403 NOT_CONTRACT_TARGET).
- RF2: solo en open; versión opcional como en CON-06 → 409.
- RF3: pasa a cancelled con closed_reason=rejected y rejected_by (la constitución no tiene estado rejected; open→cancelled es transición válida).
- RF4: no mueve puntos (la fianza solo se retiene al aceptar).
- RF5: audita `contract.rejected` e invalida la caché del grupo.

## 3. Errores

| HTTP | `code` | Causa |
|---|---|---|
| 401 | `UNAUTHORIZED` | Sin token |
| 403 | `NOT_CONTRACT_TARGET` | No es el retado |
| 409 | `INVALID_CONTRACT_STATE` | No open o versión distinta |

## 4. Implementación

- `Pactogg.Api/Modules/Contracts/ContractService.cs (RejectAsync)`
- `Pactogg.Api/Infrastructure/Mongo/Repositories/MongoContractRepository.cs (TryRejectAsync)`

## 5. Criterios de aceptación

| ID | Escenario | Evidencia |
|---|---|---|
| CON07-AC1 | Retador intenta rechazar → 403 | SocialFlowTests |
| CON07-AC2 | version 99 → 409; version 1 → 200 cancelled/rejected | SocialFlowTests |
| CON07-AC3 | Aceptar tras rechazar → 409 | SocialFlowTests |

Pruebas: `IntegrationTest/SocialFlowTests.cs` (fakes en memoria) y `SecurityTest/AuthorizationTests.cs` (401).
