# VOT-04 — POST /api/v1/contracts/{id}/votes/resolve

**Fecha:** 2026-11-04
**Estado:** Implementado y verificado localmente (pruebas en verde).
**Contrato:** `api/openapi.yaml` (v0.6.0)

## 1. Objetivo

El administrador resuelve un empate, una votación vencida o un reto sin jurados.

## 2. Requisitos funcionales

- RF1: Solo rol admin del grupo (403 NOT_GROUP_ADMIN).
- RF2: Solo en voting y si: no hay jurados, todos votaron sin consenso o pasaron las 48 h; si no → 409 VOTING_NOT_FINISHED.
- RF3: outcome fulfilled|cheating; misma transacción de cierre que VOT-02 con resolved_by=admin.

## 3. Errores

| HTTP | `code` | Causa |
|---|---|---|
| 401 | `UNAUTHORIZED` | Sin token |
| 403 | `NOT_GROUP_ADMIN` | No es admin |
| 409 | `VOTING_NOT_FINISHED / INVALID_CONTRACT_STATE` | Votación en curso o ya cerrada |
| 422 | `VALIDATION_ERROR` | outcome inválido |

## 4. Implementación

`Pactogg.Api/Modules/Votes/VoteService.cs (AdminResolveAsync)`

## 5. Criterios de aceptación

| Escenario | Evidencia |
|---|---|
| Antes de tiempo → 409 | IntegrationTest/JuryFlowTests.cs |
| Miembro no admin → 403; admin tras empate → 200 | IntegrationTest/JuryFlowTests.cs |

401 sin token: `SecurityTest/AuthorizationTests.cs`.
