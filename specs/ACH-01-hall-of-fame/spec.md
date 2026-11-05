# ACH-01 — GET /api/v1/groups/{gid}/fame

**Fecha:** 2026-11-04
**Estado:** Implementado y verificado localmente (pruebas en verde).
**Contrato:** `api/openapi.yaml` (v0.6.0)

## 1. Objetivo

ID 132 · Muro de la Fama.

## 2. Requisitos funcionales

- RF1: Miembros; retos fulfilled del grupo del más reciente al más antiguo; cursor.
- RF2: Por reto: quien lo cumplió (apodo y avatar), juego, condición, recompensa, fecha y fotos/enlaces aprobados.
- RF3: Caché 60 s por versión del grupo.

## 3. Errores

| HTTP | `code` | Causa |
|---|---|---|
| 401 | `UNAUTHORIZED` | Sin token |
| 403 | `NOT_GROUP_MEMBER` | No pertenece |

## 4. Implementación

`Pactogg.Api/Modules/Fame/FameService.cs`

## 5. Criterios de aceptación

| Escenario | Evidencia |
|---|---|
| 1 entrada tras cumplir; 0 tras trampa | IntegrationTest/JuryFlowTests.cs |

401 sin token: `SecurityTest/AuthorizationTests.cs`.
