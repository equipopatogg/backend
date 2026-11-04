# GRP-03 — GET /api/v1/groups/{id}

**Fecha:** 2026-11-04
**Estado:** Implementado y verificado localmente (pruebas de integración en verde); despliegue con `scripts/release.sh`.
**Contrato:** `api/openapi.yaml` (v0.5.0)

## 1. Objetivo

Detalle del grupo con su configuración.

## 2. Requisitos funcionales

- RF1: id inválido o grupo inexistente → 404.
- RF2: no miembro → 403 NOT_GROUP_MEMBER (sin revelar datos).
- RF3: devuelve el mismo esquema Group con my_role y my_points actuales.
- RF4: sin caché propia (2 lecturas por índice); el Excel pide 120 s: se omitió para que my_points sea exacto tras aceptar retos.

## 3. Errores

| HTTP | `code` | Causa |
|---|---|---|
| 401 | `UNAUTHORIZED` | Sin token |
| 403 | `NOT_GROUP_MEMBER` | No pertenece |
| 404 | `NOT_FOUND` | No existe |

## 4. Implementación

- `Pactogg.Api/Modules/Groups/GroupService.cs (GetAsync, RequireMembershipAsync)`

## 5. Criterios de aceptación

| ID | Escenario | Evidencia |
|---|---|---|
| GRP03-AC1 | Miembro → 200 con settings | SocialFlowTests |
| GRP03-AC2 | Ajeno → 403; inexistente → 404 | SocialFlowTests |

Pruebas: `IntegrationTest/SocialFlowTests.cs` (fakes en memoria) y `SecurityTest/AuthorizationTests.cs` (401).
