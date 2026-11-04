# GRP-02 — GET /api/v1/groups

**Fecha:** 2026-11-04
**Estado:** Implementado y verificado localmente (pruebas de integración en verde); despliegue con `scripts/release.sh`.
**Contrato:** `api/openapi.yaml` (v0.5.0)

## 1. Objetivo

Selector de grupos: los grupos activos del usuario con su rol y saldo.

## 2. Requisitos funcionales

- RF1: solo grupos donde el usuario es miembro activo.
- RF2: q filtra por texto contenido en el nombre (máx. 40, regex escapada).
- RF3: cursor por _id descendente; limit 1–50 (20 por defecto); meta {next_cursor, has_more}.
- RF4: cada grupo incluye my_role y my_points.
- RF5: caché 60 s por usuario y versión; se invalida al crear, unirse o aceptar un reto.

## 3. Errores

| HTTP | `code` | Causa |
|---|---|---|
| 401 | `UNAUTHORIZED` | Sin token |
| 401 | `UNAUTHORIZED` | Sin token |
| 422 | `VALIDATION_ERROR` | limit, cursor o q inválidos |

## 4. Implementación

- `Pactogg.Api/Modules/Groups/GroupService.cs (ListAsync)`
- `Pactogg.Api/Infrastructure/Mongo/Repositories/MongoGroupRepository.cs (ListByIdsAsync)`
- `Pactogg.Api/Common/ListQuery.cs`

## 5. Criterios de aceptación

| ID | Escenario | Evidencia |
|---|---|---|
| GRP02-AC1 | Tras unirse aparece el grupo | SocialFlowTests.Groups_JoinListDetailAndMembers |
| GRP02-AC2 | q filtra por nombre | SocialFlowTests |
| GRP02-AC3 | limit=0 → 422 | SocialFlowTests |

Pruebas: `IntegrationTest/SocialFlowTests.cs` (fakes en memoria) y `SecurityTest/AuthorizationTests.cs` (401).
