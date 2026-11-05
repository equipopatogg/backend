# EVI-03 — GET /api/v1/contracts/{id}/evidence

**Fecha:** 2026-11-04
**Estado:** Implementado y verificado localmente (pruebas en verde).
**Contrato:** `api/openapi.yaml` (v0.6.0)

## 1. Objetivo

ID 87 · Revisión de pruebas por jurado: ver las evidencias de un reto.

## 2. Requisitos funcionales

- RF1: Solo miembros del grupo del reto (403); reto inexistente → 404.
- RF2: Solo evidencias aprobadas por moderación.
- RF3: Foto → URL del CDN (CloudFront) o URL firmada de 15 min; enlace → su URL original.

## 3. Errores

| HTTP | `code` | Causa |
|---|---|---|
| 401 | `UNAUTHORIZED` | Sin token |
| 403 | `NOT_GROUP_MEMBER` | No pertenece |
| 404 | `NOT_FOUND` | No existe |

## 4. Implementación

`Pactogg.Api/Modules/Evidence/EvidenceService.cs (ListAsync)`

## 5. Criterios de aceptación

| Escenario | Evidencia |
|---|---|
| Jurado ve 1 evidencia con URL de YouTube | IntegrationTest/JuryFlowTests.cs |
| Ajeno → 403 | IntegrationTest/JuryFlowTests.cs (Outsider_…) |

401 sin token: `SecurityTest/AuthorizationTests.cs`.
