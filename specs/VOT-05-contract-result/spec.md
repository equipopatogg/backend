# VOT-05 — GET /api/v1/contracts/{id}/result

**Fecha:** 2026-11-04
**Estado:** Implementado y verificado localmente (pruebas en verde).
**Contrato:** `api/openapi.yaml` (v0.6.0)

## 1. Objetivo

Resultado final con los votos revelados.

## 2. Requisitos funcionales

- RF1: Miembros del grupo; 409 si sigue abierto.
- RF2: outcome, resolved_by, conteos, bond_movement (bond_release, bond_forfeit o none), recompensa y votos con apodo y comentario.

## 3. Errores

| HTTP | `code` | Causa |
|---|---|---|
| 401 | `UNAUTHORIZED` | Sin token |
| 403 | `NOT_GROUP_MEMBER` | No pertenece |
| 409 | `INVALID_CONTRACT_STATE` | Sin resultado |

## 4. Implementación

`Pactogg.Api/Modules/Votes/VoteService.cs (ResultAsync)`

## 5. Criterios de aceptación

| Escenario | Evidencia |
|---|---|
| Resultado fulfilled con 2 votos revelados | IntegrationTest/JuryFlowTests.cs |

401 sin token: `SecurityTest/AuthorizationTests.cs`.

## Cambio 0.7.0

- Votos revelados **anónimos** (CLARIFICACION #84): sin `juror_id` ni `nickname`, ordenados por sentido y texto y con fecha sin hora.
- `bond_movement = bond_release_and_reward` cuando se cumple (premio igual a la fianza, CLARIFICACION #89).
