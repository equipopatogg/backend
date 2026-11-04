# CON-06 — POST /api/v1/contracts/{id}/accept

**Fecha:** 2026-11-04
**Estado:** Implementado y verificado localmente (pruebas de integración en verde); despliegue con `scripts/release.sh`.
**Contrato:** `api/openapi.yaml` (v0.5.0)

## 1. Objetivo

Aceptar un reto y retener la fianza en puntos.

## 2. Requisitos funcionales

- RF1: quién: el retado si target_type=member (otro → 403 NOT_CONTRACT_TARGET); cualquier miembro salvo el retador si es de grupo.
- RF2: el retador no puede aceptar su reto → 409 CANNOT_ACCEPT_OWN_CONTRACT.
- RF3: solo en open y antes del deadline → si no, 409 INVALID_CONTRACT_STATE.
- RF4: cuerpo opcional {version}; si no coincide → 409 (concurrencia optimista).
- RF5: una transacción MongoDB (Atlas replica set): open→in_progress con versión esperada, $inc -fianza en group_members solo si points_balance ≥ fianza (nunca negativo) e inserción `bond_lock` en points_ledger (índice único ref_id+type+user_id).
- RF6: saldo insuficiente → 402 INSUFFICIENT_POINTS sin cambios; conflicto de escritura → 409.
- RF7: respuesta = reto + my_points (saldo restante). Audita `contract.accepted`; invalida cachés de grupo y usuario.

## 3. Errores

| HTTP | `code` | Causa |
|---|---|---|
| 401 | `UNAUTHORIZED` | Sin token |
| 402 | `INSUFFICIENT_POINTS` | Saldo menor que la fianza |
| 403 | `NOT_CONTRACT_TARGET / NOT_GROUP_MEMBER` | No es el retado / no pertenece |
| 404 | `NOT_FOUND` | No existe |
| 409 | `CANNOT_ACCEPT_OWN_CONTRACT / INVALID_CONTRACT_STATE` | Propio, no open, vencido o versión distinta |

## 4. Implementación

- `Pactogg.Api/Modules/Contracts/ContractService.cs (AcceptAsync)`
- `Pactogg.Api/Infrastructure/Mongo/Repositories/MongoContractRepository.cs (AcceptWithBondAsync)`
- `Pactogg.Api/Modules/Points/Domain/PointsLedgerEntry.cs`

## 5. Criterios de aceptación

| ID | Escenario | Evidencia |
|---|---|---|
| CON06-AC1 | Miembro acepta reto de grupo → in_progress, my_points 50, bond_lock en el libro | SocialFlowTests |
| CON06-AC2 | Retador → 409 CANNOT_ACCEPT_OWN_CONTRACT | SocialFlowTests |
| CON06-AC3 | Segunda vez → 409 INVALID_CONTRACT_STATE | SocialFlowTests |
| CON06-AC4 | Saldo 50 vs fianza 100 → 402 y saldo intacto | SocialFlowTests |
| CON06-AC5 | Reto dirigido a otro → 403 | SocialFlowTests.Accept_DirectedToAnotherMember_Returns403 |

Pruebas: `IntegrationTest/SocialFlowTests.cs` (fakes en memoria) y `SecurityTest/AuthorizationTests.cs` (401).
