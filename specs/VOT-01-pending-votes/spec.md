# VOT-01 — GET /api/v1/me/votes/pending

**Fecha:** 2026-11-04
**Estado:** Implementado y verificado localmente (pruebas en verde).
**Contrato:** `api/openapi.yaml` (v0.6.0)

## 1. Objetivo

ID 88: lista de retos que me toca votar.

## 2. Requisitos funcionales

- RF1: Retos en voting donde soy jurado y no he votado (índice ix_contracts_jurors_status).
- RF2: Cursor por _id descendente; limit 1–50.
- RF3: Cada elemento: el reto completo y voting_ends_at.

## 3. Errores

| HTTP | `code` | Causa |
|---|---|---|
| 401 | `UNAUTHORIZED` | Sin token |
| 422 | `VALIDATION_ERROR` | limit/cursor |

## 4. Implementación

`Pactogg.Api/Modules/Votes/VoteService.cs (ListPendingAsync)`

## 5. Criterios de aceptación

| Escenario | Evidencia |
|---|---|
| Tras la primera evidencia el jurado tiene 1 pendiente | IntegrationTest/JuryFlowTests.cs |

401 sin token: `SecurityTest/AuthorizationTests.cs`.
