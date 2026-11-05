# COM-03 — POST /api/v1/commitments/{id}/complete

**Fecha:** 2026-11-04
**Estado:** Implementado y verificado localmente (pruebas en verde).
**Contrato:** `api/openapi.yaml` (v0.6.0)

## 1. Objetivo

El acreedor confirma que le cumplieron.

## 2. Requisitos funcionales

- RF1: Solo acreedor (403 NOT_COMMITMENT_PARTY).
- RF2: pending o completion_requested → completed; si ya estaba → 409 INVALID_COMMITMENT_STATE.
- RF3: Avisa al deudor.

## 3. Errores

| HTTP | `code` | Causa |
|---|---|---|
| 401 | `UNAUTHORIZED` | Sin token |
| 403 | `NOT_COMMITMENT_PARTY` | No es acreedor |
| 404 | `NOT_FOUND` | No existe |
| 409 | `INVALID_COMMITMENT_STATE` | Ya cumplido |

## 4. Implementación

`Pactogg.Api/Modules/Commitments/CommitmentService.cs (CompleteAsync)`

## 5. Criterios de aceptación

| Escenario | Evidencia |
|---|---|
| completed y luego 409 | IntegrationTest/JuryFlowTests.cs |

401 sin token: `SecurityTest/AuthorizationTests.cs`.
