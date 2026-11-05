# COM-01 — GET /api/v1/groups/{gid}/commitments

**Fecha:** 2026-11-04
**Estado:** Implementado y verificado localmente (pruebas en verde).
**Contrato:** `api/openapi.yaml` (v0.6.0)

## 1. Objetivo

ID 124/125 · Compromisos por cumplir y a mi favor.

## 2. Requisitos funcionales

- RF1: role=debo (soy deudor, por defecto) o me-deben (soy acreedor); otro → 422.
- RF2: status pending|completion_requested|completed; cursor.
- RF3: Cada compromiso con deudor y acreedor (apodo), descripción (la recompensa) y juego. Se crean solos al cumplirse un reto (uno por reto, índice único).

## 3. Errores

| HTTP | `code` | Causa |
|---|---|---|
| 401 | `UNAUTHORIZED` | Sin token |
| 403 | `NOT_GROUP_MEMBER` | No pertenece |
| 422 | `VALIDATION_ERROR` | role/status |

## 4. Implementación

`Pactogg.Api/Modules/Commitments/CommitmentService.cs`

## 5. Criterios de aceptación

| Escenario | Evidencia |
|---|---|
| Retador ve 1 en debo; ejecutor 1 en me-deben | IntegrationTest/JuryFlowTests.cs |

401 sin token: `SecurityTest/AuthorizationTests.cs`.
