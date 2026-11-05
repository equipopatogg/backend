# PTS-01 — GET /api/v1/groups/{gid}/points/me

**Fecha:** 2026-11-04
**Estado:** Implementado y verificado localmente (pruebas en verde).
**Contrato:** `api/openapi.yaml` (v0.6.0)

## 1. Objetivo

ID 159 · Puntos sin valor monetario: saldo y movimientos propios.

## 2. Requisitos funcionales

- RF1: Miembros (403).
- RF2: balance actual + movimientos del libro (points_ledger) del más reciente al más antiguo; filtro type; cursor.
- RF3: No existe ningún endpoint de compra; los movimientos solo los crea el backend en transacciones.

## 3. Errores

| HTTP | `code` | Causa |
|---|---|---|
| 401 | `UNAUTHORIZED` | Sin token |
| 403 | `NOT_GROUP_MEMBER` | No pertenece |
| 422 | `VALIDATION_ERROR` | type inválido |

## 4. Implementación

`Pactogg.Api/Modules/Points/PointsService.cs`

## 5. Criterios de aceptación

| Escenario | Evidencia |
|---|---|
| Tras cumplir: bond_release y bond_lock, saldo 100 | IntegrationTest/JuryFlowTests.cs |
| type=compra → 422 | IntegrationTest/JuryFlowTests.cs |

401 sin token: `SecurityTest/AuthorizationTests.cs`.
