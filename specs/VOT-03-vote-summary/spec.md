# VOT-03 — GET /api/v1/contracts/{id}/votes/summary

**Fecha:** 2026-11-04
**Estado:** Implementado y verificado localmente (pruebas en verde).
**Contrato:** `api/openapi.yaml` (v0.6.0)

## 1. Objetivo

Avance de la votación sin revelar quién votó qué.

## 2. Requisitos funcionales

- RF1: Miembros del grupo.
- RF2: jurors_assigned, votes_cast, votes_remaining, voting_ends_at, i_am_juror, i_voted, admin_can_resolve.
- RF3: legit_votes / cheat_votes solo cuando el reto ya cerró.

## 3. Errores

| HTTP | `code` | Causa |
|---|---|---|
| 401 | `UNAUTHORIZED` | Sin token |
| 403 | `NOT_GROUP_MEMBER` | No pertenece |

## 4. Implementación

`Pactogg.Api/Modules/Votes/VoteService.cs (SummaryAsync)`

## 5. Criterios de aceptación

| Escenario | Evidencia |
|---|---|
| Abierto: sin conteos por lado | IntegrationTest/JuryFlowTests.cs |
| Empate: admin_can_resolve=true | IntegrationTest/JuryFlowTests.cs |

401 sin token: `SecurityTest/AuthorizationTests.cs`.
