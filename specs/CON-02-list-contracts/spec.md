# CON-02 — GET /api/v1/groups/{gid}/contracts

**Fecha:** 2026-11-04
**Estado:** Implementado y verificado localmente (pruebas de integración en verde); despliegue con `scripts/release.sh`.
**Contrato:** `api/openapi.yaml` (v0.5.0)

## 1. Objetivo

Tablero de retos del grupo con filtros.

## 2. Requisitos funcionales

- RF1: solo miembros (403); grupo inexistente → 404.
- RF2: status ∈ open, in_progress, voting, fulfilled, cheating, expired, cancelled (otro → 422).
- RF3: mine=true → retador, retado o quien aceptó.
- RF4: game exacto sin distinguir mayúsculas; q en condición, recompensa o juego (máx. 60, escapado).
- RF5: del más reciente al más antiguo con cursor; limit 1–50.
- RF6: caché 30 s por versión del grupo; se invalida al crear, aceptar, rechazar o subir evidencia.

## 3. Errores

| HTTP | `code` | Causa |
|---|---|---|
| 401 | `UNAUTHORIZED` | Sin token |
| 403 | `NOT_GROUP_MEMBER` | No pertenece |
| 422 | `VALIDATION_ERROR` | Filtros inválidos |

## 4. Implementación

- `Pactogg.Api/Modules/Contracts/ContractService.cs (ListAsync)`
- `Pactogg.Api/Infrastructure/Mongo/Repositories/MongoContractRepository.cs (ListForGroupAsync)`
- `Pactogg.Api/Índice ix_contracts_group_created`

## 5. Criterios de aceptación

| ID | Escenario | Evidencia |
|---|---|---|
| CON02-AC1 | Filtros status/game/q/mine | SocialFlowTests.Contracts_BoardDetailAcceptAndReject |
| CON02-AC2 | Tras aceptar, status=in_progress lo muestra (caché invalidada) | SocialFlowTests |
| CON02-AC3 | status=foo → 422; ajeno → 403 | SocialFlowTests |

Pruebas: `IntegrationTest/SocialFlowTests.cs` (fakes en memoria) y `SecurityTest/AuthorizationTests.cs` (401).
