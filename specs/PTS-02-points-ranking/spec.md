# PTS-02 — GET /api/v1/groups/{gid}/points/ranking

**Fecha:** 2026-11-04
**Estado:** Implementado y verificado localmente (pruebas en verde).
**Contrato:** `api/openapi.yaml` (v0.6.0)

## 1. Objetivo

Ranking del grupo por puntos.

## 2. Requisitos funcionales

- RF1: Miembros; orden por saldo desc y antigüedad; position 1..n; limit 1–50.
- RF2: Caché 60 s por versión del grupo (índice ix_group_members_group_points).

## 3. Errores

| HTTP | `code` | Causa |
|---|---|---|
| 401 | `UNAUTHORIZED` | Sin token |
| 403 | `NOT_GROUP_MEMBER` | No pertenece |

## 4. Implementación

`Pactogg.Api/Modules/Points/PointsService.cs (RankingAsync)`

## 5. Criterios de aceptación

| Escenario | Evidencia |
|---|---|
| 5 miembros con posición | IntegrationTest/JuryFlowTests.cs |

401 sin token: `SecurityTest/AuthorizationTests.cs`.
