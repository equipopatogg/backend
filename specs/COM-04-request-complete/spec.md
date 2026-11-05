# COM-04 — POST /api/v1/commitments/{id}/request-complete

**Fecha:** 2026-11-04
**Estado:** Implementado y verificado localmente (pruebas en verde).
**Contrato:** `api/openapi.yaml` (v0.6.0)

## 1. Objetivo

El deudor avisa que ya cumplió.

## 2. Requisitos funcionales

- RF1: Solo deudor (403).
- RF2: pending → completion_requested; otro estado → 409.
- RF3: Avisa al acreedor.

## 3. Errores

| HTTP | `code` | Causa |
|---|---|---|
| 401 | `UNAUTHORIZED` | Sin token |
| 403 | `NOT_COMMITMENT_PARTY` | No es deudor |
| 409 | `INVALID_COMMITMENT_STATE` | No pendiente |

## 4. Implementación

`Pactogg.Api/Modules/Commitments/CommitmentService.cs (RequestCompleteAsync)`

## 5. Criterios de aceptación

| Escenario | Evidencia |
|---|---|
| Acreedor intenta → 403; deudor → 200 | IntegrationTest/JuryFlowTests.cs |

401 sin token: `SecurityTest/AuthorizationTests.cs`.
